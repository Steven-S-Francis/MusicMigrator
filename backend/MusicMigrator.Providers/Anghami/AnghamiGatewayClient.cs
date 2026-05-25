using System.Text.Json;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiGatewayClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AnghamiGatewayClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private string BuildUrl(string type, string sid, string fingerprint,
        string? playlistId = null, params (string Key, string Value)[] extraParams)
    {
        var url = $"gateway.php?type={type}&lang=en&language=en&output=jsonhp" +
                  $"&fingerprint={fingerprint}&web2=true" +
                  $"&sid={sid}&angh_type={type}";

        if (playlistId is not null)
            url += $"&playlistid={playlistId}&buffered=1&extras=";

        foreach (var (key, val) in extraParams)
            url += $"&{Uri.EscapeDataString(key)}={Uri.EscapeDataString(val)}";

        return url;
    }

    private HttpRequestMessage CreateRequest(string url, string cookies)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Cookie", cookies);
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Referer", "https://play.anghami.com/");
        request.Headers.TryAddWithoutValidation("Origin", "https://play.anghami.com");
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        return request;
    }

    private static JsonElement ParseResponse(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("jsonHp(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")"))
            trimmed = trimmed.Substring(7, trimmed.Length - 8);
        return JsonSerializer.Deserialize<JsonElement>(trimmed);
    }

    public async Task<List<Playlist>> GetPlaylistsAsync(string cookies, string sid, string fingerprint, CancellationToken ct)
    {
        var url = BuildUrl("GETplaylists", sid, fingerprint);
        using var request = CreateRequest(url, cookies);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var json = ParseResponse(content);

        var playlists = new List<Playlist>();

        if (json.TryGetProperty("sections", out var sections))
        {
            foreach (var section in sections.EnumerateArray())
            {
                if (!section.TryGetProperty("data", out var data))
                    continue;
                if (!section.TryGetProperty("type", out var type) || type.GetString() != "playlist")
                    continue;

                foreach (var item in data.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                        continue;

                    var count = item.TryGetProperty("count", out var countEl) ? countEl.GetInt32() : 0;

                    playlists.Add(new Playlist(
                        Id: id,
                        Name: name,
                        Description: null,
                        TrackCount: count,
                        CoverUrl: null));
                }
            }
        }

        // Deduplicate: group by normalized name, keep highest track count
        return playlists
            .GroupBy(p => p.Name.Trim().ToLowerInvariant())
            .Select(g => g.OrderByDescending(p => p.TrackCount).First())
            .ToList();
    }

    public async Task<List<Track>> GetPlaylistTracksAsync(
        string playlistId, string cookies, string sid, string fingerprint, CancellationToken ct)
    {
        var url = BuildUrl("GETplaylistdata", sid, fingerprint, playlistId);
        using var request = CreateRequest(url, cookies);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var json = ParseResponse(content);

        var tracks = new List<Track>();

        if (!json.TryGetProperty("sections", out var sections))
            return tracks;

        foreach (var section in sections.EnumerateArray())
        {
            if (!section.TryGetProperty("type", out var type) || type.GetString() != "song")
                continue;
            if (!section.TryGetProperty("data", out var data))
                continue;

            foreach (var song in data.EnumerateArray())
            {
                var id = song.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var title = song.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
                var artist = song.TryGetProperty("artist", out var artistEl) ? artistEl.GetString() : null;
                var album = song.TryGetProperty("album", out var albumEl) ? albumEl.GetString() : null;
                var durationStr = song.TryGetProperty("duration", out var durEl) ? durEl.GetString() : "0";

                double durationSec = 0;
                double.TryParse(durationStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out durationSec);

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                tracks.Add(new Track(
                    Id: id,
                    Title: title ?? string.Empty,
                    Artist: artist ?? string.Empty,
                    Album: album,
                    DurationMs: (int)(durationSec * 1000),
                    IsrcCode: null));
            }
        }

        return tracks;
    }
}
