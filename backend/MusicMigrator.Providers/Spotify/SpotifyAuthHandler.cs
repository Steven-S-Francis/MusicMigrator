using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;

namespace MusicMigrator.Providers.Spotify;

public class SpotifyAuthHandler(IConfiguration configuration)
{
    private readonly string _clientId = configuration["Spotify:ClientId"]!;
    private readonly string _redirectUri = configuration["Spotify:RedirectUri"]!;

    private static readonly List<string> RequiredScopes =
    [
        "playlist-read-private",
        "playlist-read-collaborative",
        "playlist-modify-public",
        "playlist-modify-private",
        "user-library-read",
        "user-read-email"
    ];

    public (string AuthUrl, string CodeVerifier) BuildAuthorizationUrl(string state)
    {
        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        var loginRequest = new LoginRequest(
            new Uri(_redirectUri),
            _clientId,
            LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = challenge,
            State = state,
            Scope = RequiredScopes
        };

        return (loginRequest.ToUri().ToString(), verifier);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> ExchangeCodeAsync(
        string code, string codeVerifier)
    {
        var response = await new OAuthClient().RequestToken(
            new PKCETokenRequest(_clientId, code, new Uri(_redirectUri), codeVerifier));

        return (
            response.AccessToken,
            response.RefreshToken,
            DateTime.UtcNow.AddSeconds(response.ExpiresIn));
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> RefreshAsync(
        string refreshToken)
    {
        var response = await new OAuthClient().RequestToken(
            new PKCETokenRefreshRequest(_clientId, refreshToken));

        return (
            response.AccessToken,
            response.RefreshToken,
            DateTime.UtcNow.AddSeconds(response.ExpiresIn));
    }
}
