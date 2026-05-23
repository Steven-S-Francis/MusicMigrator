using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MusicMigrator.Core.Models;

namespace MusicMigrator.Providers.Anghami;

public class AnghamiApiClient(HttpClient httpClient)
{
    public async Task<List<Playlist>> GetUserPlaylistsAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/playlists/user?page_size=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var playlists = new List<Playlist>();
        string? nextPageToken = null;

        do
        {
            var url = "/v1/playlists/user?page_size=50";
            if (nextPageToken is not null)
                url += $"&page_token={Uri.EscapeDataString(nextPageToken)}";

            request.RequestUri = new Uri(url, UriKind.Relative);
            var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var items = json.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                playlists.Add(new Playlist(
                    Id: item.GetProperty("id").GetProperty("value").GetString() ?? string.Empty,
                    Name: item.GetProperty("title").GetString() ?? string.Empty,
                    Description: item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    TrackCount: item.TryGetProperty("item_count", out var count) ? count.GetInt32() : 0,
                    CoverUrl: item.TryGetProperty("artwork_url", out var art) ? art.GetString() : null));
            }

            nextPageToken = json.TryGetProperty("next_page_token", out var token)
                ? token.GetString()
                : null;
        } while (nextPageToken is not null);

        return playlists;
    }

    public async Task<List<Track>> GetPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/playlists/{playlistId}?page_size=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var tracks = new List<Track>();
        string? nextPageToken = null;

        do
        {
            var url = $"/v1/playlists/{playlistId}?page_size=50";
            if (nextPageToken is not null)
                url += $"&page_token={Uri.EscapeDataString(nextPageToken)}";

            request.RequestUri = new Uri(url, UriKind.Relative);
            var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            if (json.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("song_id", out var songId))
                        continue;

                    var id = songId.GetProperty("value").GetString();
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    tracks.Add(new Track(
                        Id: id,
                        Title: item.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                        Artist: item.TryGetProperty("artist_name", out var artist) ? artist.GetString() ?? string.Empty : string.Empty,
                        Album: item.TryGetProperty("album_name", out var album) ? album.GetString() : null,
                        DurationMs: item.TryGetProperty("duration_ms", out var dur) ? dur.GetInt32() : 0,
                        IsrcCode: null));
                }
            }

            nextPageToken = json.TryGetProperty("next_page_token", out var token)
                ? token.GetString()
                : null;
        } while (nextPageToken is not null);

        return tracks;
    }

    public async Task<List<Track>> SearchTracksAsync(string accessToken, string query, string? market, CancellationToken ct)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        market ??= "SA";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/v1/discovery/search?query={encodedQuery}&page_size=5&market={market}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var tracks = new List<Track>();

        if (json.TryGetProperty("results", out var results))
        {
            foreach (var item in results.EnumerateArray())
            {
                if (!item.TryGetProperty("song_id", out var songId))
                    continue;

                var id = songId.GetProperty("value").GetString();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                tracks.Add(new Track(
                    Id: id,
                    Title: item.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                    Artist: item.TryGetProperty("artist_name", out var artist) ? artist.GetString() ?? string.Empty : string.Empty,
                    Album: item.TryGetProperty("album_name", out var album) ? album.GetString() : null,
                    DurationMs: item.TryGetProperty("duration_ms", out var dur) ? dur.GetInt32() : 0,
                    IsrcCode: null));
            }
        }

        return tracks;
    }
}
