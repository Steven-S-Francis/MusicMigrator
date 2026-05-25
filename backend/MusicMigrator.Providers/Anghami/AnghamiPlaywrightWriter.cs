using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiPlaywrightWriter : IAsyncDisposable
{
    private readonly ILogger<AnghamiPlaywrightWriter> _logger;
    private static readonly HttpClient _httpClient = new();

    private List<BrowserContextCookiesResult>? _sessionCookies;

    // Lazy Playwright for write operations only
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly object _writeInitLock = new();
    private bool _writeInitialized;

    public AnghamiPlaywrightWriter(ILogger<AnghamiPlaywrightWriter> logger)
    {
        _logger = logger;
    }

    // --- Cookie management ---

    public void SetSessionCookies(string cookieString)
    {
        var cookies = new List<BrowserContextCookiesResult>();
        foreach (var segment in cookieString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIdx = segment.IndexOf('=');
            if (eqIdx < 0) continue;
            var name = segment[..eqIdx].Trim();
            var value = segment[(eqIdx + 1)..].Trim();
            if (string.IsNullOrEmpty(name)) continue;
            cookies.Add(new BrowserContextCookiesResult
            {
                Name = name, Value = value, Domain = ".anghami.com", Path = "/",
                Secure = true, HttpOnly = false, SameSite = SameSiteAttribute.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
            });
        }
        _sessionCookies = cookies;
        _logger.LogInformation("Session cookies set: {Count} cookies", _sessionCookies.Count);
    }

    public bool IsLoggedInAsync()
    {
        return _sessionCookies is not null && _sessionCookies.Count > 0;
    }

    // --- Helpers ---

    private string BuildCookieHeader()
    {
        if (_sessionCookies is null) return "";
        return string.Join("; ", _sessionCookies.Select(c => $"{c.Name}={c.Value}"));
    }

    private string? GetCookieValue(string name)
    {
        return _sessionCookies?.FirstOrDefault(c => c.Name == name)?.Value;
    }

    private string? GetFingerprint()
    {
        var fpCookie = GetCookieValue("fingerprint");
        if (fpCookie is null) return null;
        try
        {
            var bytes = Convert.FromBase64String(fpCookie);
            var json = Encoding.UTF8.GetString(bytes);
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("fp").GetString();
        }
        catch { return null; }
    }

    private string? GetSid()
    {
        var appsid = GetCookieValue("appsidsave");
        if (appsid is null) return null;
        return Uri.UnescapeDataString(appsid);
    }

    private string BuildGatewayUrl(string type, string? extraParams = null)
    {
        var fingerprint = GetFingerprint() ?? "";
        var sid = GetSid() ?? "";
        var url = $"https://coussa.anghami.com/gateway.php?type={Uri.EscapeDataString(type)}&lang=en&language=en&output=jsonhp&fingerprint={Uri.EscapeDataString(fingerprint)}&web2=true&sid={Uri.EscapeDataString(sid)}&angh_type={Uri.EscapeDataString(type)}";
        if (extraParams is not null)
            url += "&" + extraParams;
        return url;
    }

    private async Task<string> CallGatewayAsync(string type, string? extraParams = null)
    {
        var url = BuildGatewayUrl(type, extraParams);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.Add("Referer", "https://play.anghami.com/");
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
        request.Headers.Add("Cookie", BuildCookieHeader());

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // --- Read methods via HTTP API ---

    public async Task<List<Playlist>> GetPlaylistsAsync()
    {
        try
        {
            var json = await CallGatewayAsync("GETplaylists");
            var doc = JsonDocument.Parse(json);
            var playlists = new List<Playlist>();

            foreach (var section in doc.RootElement.GetProperty("sections").EnumerateArray())
            {
                if (!section.TryGetProperty("data", out var data)) continue;
                foreach (var item in data.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString();
                    var name = item.GetProperty("name").GetString() ?? "Unknown";
                    var count = item.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                    var coverUrl = item.TryGetProperty("coverArtImage", out var img) ? img.GetString() : null;

                    playlists.Add(new Playlist(id!, name, null, count, coverUrl));
                }
            }

            return playlists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Anghami playlists via API");
            return [];
        }
    }

    public async Task<List<Track>> GetPlaylistTracksAsync(string playlistId)
    {
        try
        {
            var json = await CallGatewayAsync("GETplaylistdata", $"playlistid={playlistId}&buffered=1&extras=");
            var doc = JsonDocument.Parse(json);
            var tracks = new List<Track>();

            foreach (var section in doc.RootElement.GetProperty("sections").EnumerateArray())
            {
                if (!section.TryGetProperty("type", out var typeElem) || typeElem.GetString() != "song") continue;
                if (!section.TryGetProperty("data", out var data)) continue;

                foreach (var item in data.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString();
                    var title = item.GetProperty("title").GetString() ?? "Unknown";
                    var artist = item.GetProperty("artist").GetString() ?? "Unknown";
                    var album = item.TryGetProperty("album", out var a) ? a.GetString() : null;
                    var durationStr = item.TryGetProperty("duration", out var d) ? d.GetString() : "0";

                    var durationMs = 0;
                    if (double.TryParse(durationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var secs))
                        durationMs = (int)(secs * 1000);

                    tracks.Add(new Track(id!, title, artist, album, durationMs, null));
                }
            }

            return tracks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Anghami playlist tracks via API");
            return [];
        }
    }

    public async Task<Track?> SearchTrackAsync(string title, string artist)
    {
        try
        {
            var query = Uri.EscapeDataString($"{title} {artist}");
            var json = await CallGatewayAsync("search", $"q={query}&page_size=5");
            var doc = JsonDocument.Parse(json);

            foreach (var section in doc.RootElement.GetProperty("sections").EnumerateArray())
            {
                if (!section.TryGetProperty("type", out var typeElem) || typeElem.GetString() != "song") continue;
                if (!section.TryGetProperty("data", out var data)) continue;

                foreach (var item in data.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString();
                    var resultTitle = item.GetProperty("title").GetString() ?? title;
                    var resultArtist = item.GetProperty("artist").GetString() ?? artist;

                    return new Track(id!, resultTitle, resultArtist, null, 0, null);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search Anghami for '{Title} {Artist}'", title, artist);
            return null;
        }
    }

    // --- Write methods via Playwright (lazy init) ---

    private async Task EnsureWriteInitializedAsync()
    {
        if (_writeInitialized) return;
        lock (_writeInitLock)
        {
            if (_writeInitialized) return;
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Channel = "chrome"
        });

        _writeInitialized = true;
    }

    private async Task<IBrowserContext> CreateAuthenticatedContextAsync()
    {
        var context = await _browser!.NewContextAsync();
        if (_sessionCookies is not null && _sessionCookies.Count > 0)
        {
            await context.AddCookiesAsync(_sessionCookies.Select(c => new Cookie
            {
                Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path,
                Expires = c.Expires, HttpOnly = c.HttpOnly, Secure = c.Secure, SameSite = c.SameSite
            }));
        }
        return context;
    }

    public async Task<string> CreatePlaylistAsync(string name, string? description)
    {
        await EnsureWriteInitializedAsync();

        IBrowserContext? context = null;
        try
        {
            context = await CreateAuthenticatedContextAsync();
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

    public async Task AddTrackToPlaylistAsync(string playlistId, string songId)
    {
        await EnsureWriteInitializedAsync();

        IBrowserContext? context = null;
        try
        {
            context = await CreateAuthenticatedContextAsync();
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

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();
    }
}
