using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiPlaywrightWriter : IAsyncDisposable
{
    private readonly ILogger<AnghamiPlaywrightWriter> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly object _initLock = new();
    private bool _initialized;
    private System.Diagnostics.Process? _chromeProcess;
    private List<BrowserContextCookiesResult>? _sessionCookies;

    public AnghamiPlaywrightWriter(ILogger<AnghamiPlaywrightWriter> logger)
    {
        _logger = logger;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        lock (_initLock)
        {
            if (_initialized)
                return;
            _initialized = true;
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        var chromePaths = new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                @"Google\Chrome\Application\chrome.exe")
        };

        var chromePath = chromePaths.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException(
                "Chrome not found. Please install Google Chrome.");

        _chromeProcess = System.Diagnostics.Process.Start(
            new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = "--remote-debugging-port=9222 " +
                            "--no-first-run " +
                            "--no-default-browser-check",
                UseShellExecute = false
            });

        await Task.Delay(3000);

        _browser = await _playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
    }

    public async Task<bool> LoginAsync(string? email = null, string? password = null)
    {
        await EnsureInitializedAsync();

        IBrowserContext? context = null;
        try
        {
            context = await _browser!.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync("https://landing.anghami.com", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            _logger.LogInformation("Anghami browser window opened. Waiting for user to log in manually...");

            const int maxIterations = 150; // 5 minutes at 2s per iteration
            for (int i = 0; i < maxIterations; i++)
            {
                await page.WaitForTimeoutAsync(2000);

                if (await page.Locator("a:has-text('Logout')").IsVisibleAsync())
                {
                    var cookies = await context.CookiesAsync();
                    _sessionCookies = [.. cookies];
                    _logger.LogInformation("Login detected, {Count} cookies captured", _sessionCookies.Count);
                    return true;
                }
            }

            _logger.LogInformation("Login timeout - user did not log in within 5 minutes");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anghami manual login error: {Message}", ex.Message);
            return false;
        }
        finally
        {
            if (context is not null)
                await context.DisposeAsync();
        }
    }

    public async Task<List<Playlist>> GetPlaylistsAsync()
    {
        await EnsureInitializedAsync();
        var playlists = new List<Playlist>();

        IBrowserContext? context = null;
        try
        {
            context = await CreateAuthenticatedContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync("https://open.anghami.com/library", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.WaitForSelectorAsync("a[href*=\"/playlist/\"]", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            var items = await page.Locator("a[href*=\"/playlist/\"]").AllAsync();

            foreach (var item in items)
            {
                var href = await item.GetAttributeAsync("href");
                if (string.IsNullOrWhiteSpace(href)) continue;

                var id = href.Trim('/').Split('/').Last();
                var name = await item.Locator("[class*=\"title\"], [class*=\"name\"]").First.TextContentAsync();
                var trackCountText = await item.Locator("[class*=\"count\"], [class*=\"tracks\"]").First.TextContentAsync();
                var coverUrl = await item.Locator("img").First.GetAttributeAsync("src");

                int? trackCount = null;
                if (!string.IsNullOrWhiteSpace(trackCountText))
                {
                    var digits = new string(trackCountText.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out var count))
                        trackCount = count;
                }

                playlists.Add(new Playlist(
                    id,
                    (name ?? "Unknown").Trim(),
                    null,
                    trackCount ?? 0,
                    coverUrl));
            }

            return playlists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Anghami playlists");
            return playlists;
        }
        finally
        {
            if (context is not null)
                await context.DisposeAsync();
        }
    }

    public async Task<List<Track>> GetPlaylistTracksAsync(string playlistId)
    {
        await EnsureInitializedAsync();
        var tracks = new List<Track>();

        IBrowserContext? context = null;
        try
        {
            context = await CreateAuthenticatedContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync($"https://open.anghami.com/playlist/{playlistId}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.WaitForSelectorAsync("[class*=\"track\"], [class*=\"song\"]", new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            var previousCount = 0;
            for (var i = 0; i < 10; i++)
            {
                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                await page.WaitForTimeoutAsync(1500);

                var currentCount = await page.Locator("[class*=\"track\"], [class*=\"song\"]").CountAsync();
                if (currentCount == previousCount) break;
                previousCount = currentCount;
            }

            var rows = await page.Locator("[class*=\"track\"], [class*=\"song\"]").AllAsync();

            foreach (var row in rows)
            {
                try
                {
                    var title = await row.Locator("[class*=\"title\"]").First.TextContentAsync();
                    var artist = await row.Locator("[class*=\"artist\"]").First.TextContentAsync();
                    var durationText = await row.Locator("[class*=\"duration\"]").First.TextContentAsync();

                    var href = await row.Locator("a").First.GetAttributeAsync("href");
                    var id = !string.IsNullOrWhiteSpace(href) ? href.Trim('/').Split('/').Last() : Guid.NewGuid().ToString();

                    var durationMs = 0;
                    if (!string.IsNullOrWhiteSpace(durationText))
                    {
                        var parts = durationText.Trim().Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0], out var min) && int.TryParse(parts[1], out var sec))
                            durationMs = (min * 60 + sec) * 1000;
                    }

                    tracks.Add(new Track(
                        id,
                        (title ?? "Unknown").Trim(),
                        (artist ?? "Unknown").Trim(),
                        null,
                        durationMs,
                        null));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse a track row from Anghami playlist");
                }
            }

            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Anghami playlist tracks");
            return tracks;
        }
        finally
        {
            if (context is not null)
                await context.DisposeAsync();
        }
    }

    public async Task<Track?> SearchTrackAsync(string title, string artist)
    {
        await EnsureInitializedAsync();

        IBrowserContext? context = null;
        try
        {
            context = await CreateAuthenticatedContextAsync();
            var page = await context.NewPageAsync();

            var query = Uri.EscapeDataString($"{title} {artist}");
            await page.GotoAsync($"https://open.anghami.com/search/{query}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.WaitForSelectorAsync("[class*=\"track\"], [class*=\"song\"]", new PageWaitForSelectorOptions
            {
                Timeout = 8000
            });

            var firstRow = page.Locator("[class*=\"track\"], [class*=\"song\"]").First;
            if (!await firstRow.IsVisibleAsync())
                return null;

            var resultTitle = await firstRow.Locator("[class*=\"title\"]").First.TextContentAsync();
            var resultArtist = await firstRow.Locator("[class*=\"artist\"]").First.TextContentAsync();
            var href = await firstRow.Locator("a").First.GetAttributeAsync("href");
            var id = !string.IsNullOrWhiteSpace(href) ? href.Trim('/').Split('/').Last() : Guid.NewGuid().ToString();

            return new Track(
                id,
                (resultTitle ?? title).Trim(),
                (resultArtist ?? artist).Trim(),
                null,
                0,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search Anghami for '{Title} {Artist}'", title, artist);
            return null;
        }
        finally
        {
            if (context is not null)
                await context.DisposeAsync();
        }
    }

    public bool IsLoggedInAsync()
    {
        return _sessionCookies is not null && _sessionCookies.Count > 0;
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string name, string? description)
    {
        await EnsureInitializedAsync();

        IBrowserContext? context = null;
        try
        {
            context = await _browser!.NewContextAsync();

            await context.AddCookiesAsync([
                new Cookie
                {
                    Name = "anghami_access_token",
                    Value = accessToken,
                    Domain = ".anghami.com",
                    Path = "/",
                    Secure = true,
                    SameSite = SameSiteAttribute.None
                }
            ]);

            var page = await context.NewPageAsync();
            await page.GotoAsync("https://open.anghami.com/library", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.GetByTestId("create-playlist-button").First.ClickAsync();
            await page.GetByRole(AriaRole.Textbox).First.FillAsync(name);

            if (!string.IsNullOrEmpty(description))
            {
                var descField = page.GetByRole(AriaRole.Textbox).Nth(1);
                if (await descField.IsVisibleAsync())
                    await descField.FillAsync(description);
            }

            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" }).ClickAsync();

            await page.WaitForURLAsync("**/playlist/**");

            var playlistId = page.Url.Split('/').Last().Split('?').First();
            return playlistId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Anghami playlist via Playwright");
            throw;
        }
        finally
        {
            if (context is not null)
                await context.DisposeAsync();
        }
    }

    public async Task AddTrackToPlaylistAsync(string accessToken, string playlistId, string songId)
    {
        await EnsureInitializedAsync();

        IBrowserContext? context = null;
        try
        {
            context = await _browser!.NewContextAsync();

            await context.AddCookiesAsync([
                new Cookie
                {
                    Name = "anghami_access_token",
                    Value = accessToken,
                    Domain = ".anghami.com",
                    Path = "/",
                    Secure = true,
                    SameSite = SameSiteAttribute.None
                }
            ]);

            var page = await context.NewPageAsync();
            await page.GotoAsync($"https://open.anghami.com/song/{songId}", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var moreButton = page.GetByTestId("more").Or(page.GetByLabel("more")).First;
            await moreButton.ClickAsync();

            await page.GetByText("Add to playlist").First.ClickAsync();

            var playlistItem = page.Locator($"li[data-playlist-id=\"{playlistId}\"], [data-id=\"{playlistId}\"]").First;
            if (await playlistItem.IsVisibleAsync())
            {
                await playlistItem.ClickAsync();
            }
            else
            {
                await page.GetByText(playlistId, new PageGetByTextOptions { Exact = false }).First.ClickAsync();
            }

            await page.WaitForSelectorAsync(".toast-success, [class*=\"success\"]", new PageWaitForSelectorOptions
            {
                Timeout = 5000
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add track {SongId} to Anghami playlist {PlaylistId}", songId, playlistId);
        }
        finally
        {
            if (context is not null)
                await context.DisposeAsync();
        }
    }

    private async Task<IBrowserContext> CreateAuthenticatedContextAsync()
    {
        var context = await _browser!.NewContextAsync();
        if (_sessionCookies is not null && _sessionCookies.Count > 0)
        {
            await context.AddCookiesAsync(_sessionCookies.Select(c => new Cookie
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = c.Path,
                Expires = c.Expires,
                HttpOnly = c.HttpOnly,
                Secure = c.Secure,
                SameSite = c.SameSite
            }));
        }
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();

        if (_chromeProcess is not null)
        {
            try { _chromeProcess.Kill(); } catch { }
            _chromeProcess.Dispose();
        }
    }
}
