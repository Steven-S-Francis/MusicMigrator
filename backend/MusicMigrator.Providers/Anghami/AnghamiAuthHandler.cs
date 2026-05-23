using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiAuthHandler(IConfiguration configuration, HttpClient httpClient)
{
    private readonly string _clientId = configuration["Anghami:ClientId"]!;
    private readonly string _redirectUri = configuration["Anghami:RedirectUri"]!;

    private const string AuthBaseUrl = "https://sdk.anghami.com/v1/auth/authorize";
    private const string TokenUrl = "https://sdk.anghami.com/v1/auth/token";
    private const string RefreshUrl = "https://sdk.anghami.com/v1/auth/token/refresh";

    public (string AuthUrl, string CodeVerifier) BuildAuthorizationUrl(string state)
    {
        var verifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(verifier);

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["response_type"] = "code",
            ["scope"] = "read",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return ($"{AuthBaseUrl}?{queryString}", verifier);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> ExchangeCodeAsync(
        string code, string codeVerifier)
    {
        var body = new
        {
            grant_type = "authorization_code",
            code,
            redirect_uri = _redirectUri,
            client_id = _clientId,
            code_verifier = codeVerifier
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = JsonContent.Create(body, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request);
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
        var body = new
        {
            refresh_token = refreshToken,
            client_id = _clientId
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl)
        {
            Content = JsonContent.Create(body, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request);
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
