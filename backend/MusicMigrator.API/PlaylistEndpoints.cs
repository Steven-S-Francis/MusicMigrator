using MusicMigrator.Core.Interfaces;

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

            var playlists = await matchedProvider.GetPlaylistsAsync(token.AccessToken, CancellationToken.None);
            return Results.Ok(playlists);
        });
    }
}
