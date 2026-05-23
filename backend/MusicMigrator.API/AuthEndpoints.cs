using System.Text.Json;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using MusicMigrator.Providers.Anghami;
using MusicMigrator.Providers.Spotify;
using MusicMigrator.Providers.YouTube;

namespace MusicMigrator.API;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/status", async (HttpContext ctx, ITokenStore tokenStore) =>
        {
            var sessionId = GetOrCreateSession(ctx);
            return Results.Ok(new
            {
                spotify = tokenStore.IsConnected(sessionId, "spotify"),
                youtube = tokenStore.IsConnected(sessionId, "youtube"),
                anghami = tokenStore.IsConnected(sessionId, "anghami")
            });
        });

        group.MapGet("/spotify/start", async (HttpContext ctx, SpotifyAuthHandler auth) =>
        {
            var state = Guid.NewGuid().ToString();
            var (authUrl, codeVerifier) = auth.BuildAuthorizationUrl(state);
            var oauthState = new OAuthState("spotify", codeVerifier, "/");
            ctx.Session.SetString($"oauth_state_{state}", JsonSerializer.Serialize(oauthState));
            return Results.Redirect(authUrl);
        });

        group.MapGet("/spotify/callback", async (
            HttpContext ctx, string code, string state, ITokenStore tokenStore, SpotifyAuthHandler auth) =>
        {
            var oauthState = GetOAuthState(ctx, state);
            if (oauthState is null)
                return Results.BadRequest("Invalid state parameter.");

            var (accessToken, refreshToken, expiresAt) =
                await auth.ExchangeCodeAsync(code, oauthState.CodeVerifier);

            tokenStore.Store(
                GetOrCreateSession(ctx),
                "spotify",
                new ProviderToken(accessToken, refreshToken, expiresAt));

            return Results.Redirect("http://localhost:5173/?connected=spotify");
        });

        group.MapGet("/youtube/start", async (HttpContext ctx, YouTubeAuthHandler auth) =>
        {
            var state = Guid.NewGuid().ToString();
            var (authUrl, codeVerifier) = auth.BuildAuthorizationUrl(state);
            var oauthState = new OAuthState("youtube", codeVerifier, "/");
            ctx.Session.SetString($"oauth_state_{state}", JsonSerializer.Serialize(oauthState));
            return Results.Redirect(authUrl);
        });

        group.MapGet("/youtube/callback", async (
            HttpContext ctx, string code, string state, ITokenStore tokenStore, YouTubeAuthHandler auth) =>
        {
            var oauthState = GetOAuthState(ctx, state);
            if (oauthState is null)
                return Results.BadRequest("Invalid state parameter.");

            var (accessToken, refreshToken, expiresAt) =
                await auth.ExchangeCodeAsync(code, oauthState.CodeVerifier);

            tokenStore.Store(
                GetOrCreateSession(ctx),
                "youtube",
                new ProviderToken(accessToken, refreshToken, expiresAt));

            return Results.Redirect("http://localhost:5173/?connected=youtube");
        });

        group.MapGet("/anghami/start", async (HttpContext ctx, AnghamiAuthHandler auth) =>
        {
            var state = Guid.NewGuid().ToString();
            var (authUrl, codeVerifier) = auth.BuildAuthorizationUrl(state);
            var oauthState = new OAuthState("anghami", codeVerifier, "/");
            ctx.Session.SetString($"oauth_state_{state}", JsonSerializer.Serialize(oauthState));
            return Results.Redirect(authUrl);
        });

        group.MapGet("/anghami/callback", async (
            HttpContext ctx, string code, string state, ITokenStore tokenStore, AnghamiAuthHandler auth) =>
        {
            var oauthState = GetOAuthState(ctx, state);
            if (oauthState is null)
                return Results.BadRequest("Invalid state parameter.");

            var (accessToken, refreshToken, expiresAt) =
                await auth.ExchangeCodeAsync(code, oauthState.CodeVerifier);

            tokenStore.Store(
                GetOrCreateSession(ctx),
                "anghami",
                new ProviderToken(accessToken, refreshToken, expiresAt));

            return Results.Redirect("http://localhost:5173/?connected=anghami");
        });

        group.MapDelete("/{provider}", (HttpContext ctx, string provider, ITokenStore tokenStore) =>
        {
            var sessionId = GetOrCreateSession(ctx);
            tokenStore.Remove(sessionId, provider);
            return Results.Ok();
        });
    }

    private static string GetOrCreateSession(HttpContext ctx)
    {
        var sessionId = ctx.Session.GetString("session_id");
        if (sessionId is null)
        {
            sessionId = Guid.NewGuid().ToString();
            ctx.Session.SetString("session_id", sessionId);
        }
        return sessionId;
    }

    private static OAuthState? GetOAuthState(HttpContext ctx, string state)
    {
        var key = $"oauth_state_{state}";
        var json = ctx.Session.GetString(key);
        if (json is null)
            return null;

        ctx.Session.Remove(key);
        return JsonSerializer.Deserialize<OAuthState>(json);
    }
}
