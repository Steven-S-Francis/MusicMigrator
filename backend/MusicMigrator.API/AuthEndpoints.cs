using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using MusicMigrator.Core.Services;
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

        group.MapGet("/spotify/start", async (HttpContext ctx, SpotifyAuthHandler auth, OAuthStateStore oAuthStateStore) =>
        {
            var state = Guid.NewGuid().ToString();
            var (authUrl, codeVerifier) = auth.BuildAuthorizationUrl(state);
            var sessionId = GetOrCreateSession(ctx);
            oAuthStateStore.Save(state, new OAuthState("spotify", codeVerifier, "/"), sessionId);
            return Results.Redirect(authUrl);
        });

        group.MapGet("/spotify/callback", async (
            HttpContext ctx, string code, string state, ITokenStore tokenStore,
            SpotifyAuthHandler auth, OAuthStateStore oAuthStateStore) =>
        {
            var (oauthState, sessionId) = oAuthStateStore.Consume(state);
            if (oauthState is null)
                return Results.BadRequest("Invalid state.");

            var (accessToken, refreshToken, expiresAt) =
                await auth.ExchangeCodeAsync(code, oauthState.CodeVerifier);

            tokenStore.Store(sessionId!, "spotify",
                new ProviderToken(accessToken, refreshToken, expiresAt));

            return Results.Redirect("http://localhost:5173/?connected=spotify");
        });

        group.MapGet("/youtube/start", async (HttpContext ctx, YouTubeAuthHandler auth, OAuthStateStore oAuthStateStore) =>
        {
            var state = Guid.NewGuid().ToString();
            var (authUrl, codeVerifier) = auth.BuildAuthorizationUrl(state);
            var sessionId = GetOrCreateSession(ctx);
            oAuthStateStore.Save(state, new OAuthState("youtube", codeVerifier, "/"), sessionId);
            return Results.Redirect(authUrl);
        });

        group.MapGet("/youtube/callback", async (
            HttpContext ctx, string code, string state, ITokenStore tokenStore,
            YouTubeAuthHandler auth, OAuthStateStore oAuthStateStore) =>
        {
            var (oauthState, sessionId) = oAuthStateStore.Consume(state);
            if (oauthState is null)
                return Results.BadRequest("Invalid state.");

            var (accessToken, refreshToken, expiresAt) =
                await auth.ExchangeCodeAsync(code, oauthState.CodeVerifier);

            tokenStore.Store(sessionId!, "youtube",
                new ProviderToken(accessToken, refreshToken, expiresAt));

            return Results.Redirect("http://localhost:5173/?connected=youtube");
        });

        group.MapPost("/anghami/cookies", async (
            HttpContext ctx, CookieRequest body, ITokenStore tokenStore, AnghamiPlaywrightWriter writer) =>
        {
            var sessionId = GetOrCreateSession(ctx);
            if (body.Cookies is null || body.Cookies.Count == 0)
                return Results.Json(new { success = false, error = "No cookies provided." }, statusCode: 400);

            writer.SetSessionCookies(body.Cookies);

            tokenStore.Store(sessionId, "anghami",
                new ProviderToken("session-cookies", null, DateTime.UtcNow.AddHours(8)));

            return Results.Ok(new { success = true });
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
}

public record CookieRequest(List<CookieInput> Cookies);
