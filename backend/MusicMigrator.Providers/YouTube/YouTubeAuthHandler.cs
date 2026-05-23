using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MusicMigrator.Providers.YouTube;

public class YouTubeAuthHandler(IConfiguration configuration, HttpClient httpClient)
{
    private readonly string _clientId = configuration["YouTube:ClientId"]!;
    private readonly string _clientSecret = configuration["YouTube:ClientSecret"]!;
    private readonly string _redirectUri = configuration["YouTube:RedirectUri"]!;

    private const string AuthBaseUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";

    private const string RequiredScopes =
        "https://www.googleapis.com/auth/youtube https://www.googleapis.com/auth/youtube.force-ssl";

    public (string AuthUrl, string CodeVerifier) BuildAuthorizationUrl(string state)
    {
        var verifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(verifier);

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["response_type"] = "code",
            ["scope"] = RequiredScopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var queryString = string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return ($"{AuthBaseUrl}?{queryString}", verifier);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> ExchangeCodeAsync(
        string code, string codeVerifier)
    {
        var body = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        };

        var response = await httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(body));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (
            json.GetProperty("access_token").GetString()!,
            json.GetProperty("refresh_token").GetString()!,
            DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()));
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> RefreshAsync(
        string refreshToken)
    {
        var body = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["grant_type"] = "refresh_token"
        };

        var response = await httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(body));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (
            json.GetProperty("access_token").GetString()!,
            refreshToken,
            DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()));
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
