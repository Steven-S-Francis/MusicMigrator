using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiPlaywrightWriter : IAsyncDisposable
{
    private readonly ILogger<AnghamiPlaywrightWriter> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly object _initLock = new();
    private bool _initialized;

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
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
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

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();
    }
}
