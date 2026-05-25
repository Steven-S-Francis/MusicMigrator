using System.Net;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using MusicMigrator.Providers.Spotify;
using MusicMigrator.Providers.YouTube;

namespace MusicMigrator.API;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/playlists").WithTags("Playlists");

        group.MapGet("/{provider}", async (
            HttpContext ctx,
            string provider,
            ITokenStore tokenStore,
            SpotifyAuthHandler spotifyAuth,
            YouTubeAuthHandler youtubeAuth,
            IEnumerable<IMusicProvider> providers) =>
        {
            var sessionId = ctx.Session.GetString("session_id");
            if (sessionId is null)
                return Results.Unauthorized();

            var token = tokenStore.Get(sessionId, provider);
            if (token is null)
                return Results.Unauthorized();

            var matchedProvider = providers.FirstOrDefault(
                p => p.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (matchedProvider is null)
                return Results.BadRequest($"Unknown provider '{provider}'.");

            try
            {
                token = await RefreshIfExpiredAsync(token, sessionId, provider, tokenStore, spotifyAuth, youtubeAuth);
                var playlists = await matchedProvider.GetPlaylistsAsync(token.AccessToken, CancellationToken.None);
                return Results.Ok(playlists);
            }
            catch (Exception ex) when (ex is SpotifyAPI.Web.APIException or Google.GoogleApiException)
            {
                var statusCode = ex switch
                {
                    SpotifyAPI.Web.APIException sp => (int?)(sp.Response?.StatusCode ?? 0),
                    Google.GoogleApiException ggl => (int?)ggl.HttpStatusCode,
                    _ => null
                };

                if (statusCode == 401 && token.RefreshToken is not null)
                {
                    var refreshed = await TryRefreshAsync(token, sessionId, provider, tokenStore, spotifyAuth, youtubeAuth);
                    if (refreshed is not null)
                    {
                        try
                        {
                            var playlists = await matchedProvider.GetPlaylistsAsync(refreshed.AccessToken, CancellationToken.None);
                            return Results.Ok(playlists);
                        }
                        catch { }
                    }
                }

                return Results.Json(new
                {
                    error = $"Spotify API error (HTTP {statusCode ?? 0})",
                    detail = ex.Message,
                    provider,
                    action = "Reconnect this provider from the accounts page."
                }, statusCode: statusCode is >= 400 and < 500 ? (int)HttpStatusCode.BadRequest : 500);
            }
        });
    }

    private static async Task<ProviderToken> RefreshIfExpiredAsync(
        ProviderToken token,
        string sessionId,
        string provider,
        ITokenStore tokenStore,
        SpotifyAuthHandler spotifyAuth,
        YouTubeAuthHandler youtubeAuth)
    {
        if (!token.IsExpired || token.RefreshToken is null)
            return token;

        var result = await TryRefreshAsync(token, sessionId, provider, tokenStore, spotifyAuth, youtubeAuth);
        return result ?? token;
    }

    private static async Task<ProviderToken?> TryRefreshAsync(
        ProviderToken token,
        string sessionId,
        string provider,
        ITokenStore tokenStore,
        SpotifyAuthHandler spotifyAuth,
        YouTubeAuthHandler youtubeAuth)
    {
        try
        {
            var (newAccess, newRefresh, newExpires) = provider.ToLowerInvariant() switch
            {
                "spotify" => await spotifyAuth.RefreshAsync(token.RefreshToken!),
                "youtube" => await youtubeAuth.RefreshAsync(token.RefreshToken!),
                _ => (null, null, DateTime.MinValue)
            };

            if (newAccess is null)
                return null;

            var refreshed = new ProviderToken(newAccess, newRefresh, newExpires);
            tokenStore.Store(sessionId, provider, refreshed);
            return refreshed;
        }
        catch
        {
            return null;
        }
    }
}
