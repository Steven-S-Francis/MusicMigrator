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

            token = await RefreshIfExpiredAsync(token, sessionId, provider, tokenStore, spotifyAuth, youtubeAuth);

            var matchedProvider = providers.FirstOrDefault(
                p => p.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (matchedProvider is null)
                return Results.BadRequest($"Unknown provider '{provider}'.");

            var playlists = await matchedProvider.GetPlaylistsAsync(token.AccessToken, CancellationToken.None);
            return Results.Ok(playlists);
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

        var (newAccess, newRefresh, newExpires) = provider.ToLowerInvariant() switch
        {
            "spotify" => await spotifyAuth.RefreshAsync(token.RefreshToken),
            "youtube" => await youtubeAuth.RefreshAsync(token.RefreshToken),
            _ => (null, null, DateTime.MinValue)
        };

        if (newAccess is null)
            return token;

        var refreshed = new ProviderToken(newAccess, newRefresh, newExpires);
        tokenStore.Store(sessionId, provider, refreshed);
        return refreshed;
    }
}
