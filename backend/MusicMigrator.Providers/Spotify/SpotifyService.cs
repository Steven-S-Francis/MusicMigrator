using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using Track = MusicMigrator.Core.Models.Track;
using Playlist = MusicMigrator.Core.Models.Playlist;

namespace MusicMigrator.Providers.Spotify;

public class SpotifyService : IMusicProvider
{
    public string ProviderName => "spotify";

    private static SpotifyClient CreateClient(string accessToken) =>
        new(accessToken);

    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(string accessToken, CancellationToken ct)
    {
        var client = CreateClient(accessToken);
        var request = new PlaylistCurrentUsersRequest { Limit = 50 };
        var firstPage = await client.Playlists.CurrentUsers(request);
        var allItems = await client.PaginateAll(firstPage);

        return allItems.Select(p => new Playlist(
            Id: p.Id!,
            Name: p.Name!,
            Description: p.Description,
            TrackCount: p.Tracks?.Total ?? 0,
            CoverUrl: p.Images?.FirstOrDefault()?.Url));
    }

    public async Task<IEnumerable<Track>> GetTracksAsync(string accessToken, string playlistId, CancellationToken ct)
    {
        var client = CreateClient(accessToken);
        var request = new PlaylistGetItemsRequest();
        var firstPage = await client.Playlists.GetItems(playlistId, request);
        var allItems = await client.PaginateAll(firstPage);

        return allItems
            .Where(item => item.Track is FullTrack)
            .Select(item =>
            {
                var ft = (FullTrack)item.Track;
                string? isrc = null;
                ft.ExternalIds?.TryGetValue("isrc", out isrc);

                return new Track(
                    Id: ft.Id,
                    Title: ft.Name,
                    Artist: string.Join(", ", ft.Artists.Select(a => a.Name)),
                    Album: ft.Album?.Name,
                    DurationMs: ft.DurationMs,
                    IsrcCode: isrc);
            });
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string name, string? description, CancellationToken ct)
    {
        var client = CreateClient(accessToken);
        var user = await client.UserProfile.Current();
        var createRequest = new PlaylistCreateRequest(name)
        {
            Description = description ?? string.Empty,
            Public = false
        };
        var playlist = await client.Playlists.Create(user.Id, createRequest);
        return playlist.Id!;
    }

    public async Task AddTracksAsync(string accessToken, string playlistId, IEnumerable<Track> tracks, CancellationToken ct)
    {
        var client = CreateClient(accessToken);
        var uris = tracks.Select(t => $"spotify:track:{t.Id}").ToList();

        foreach (var batch in uris.Chunk(100))
        {
            var request = new PlaylistAddItemsRequest(batch.ToList());
            await client.Playlists.AddItems(playlistId, request);
            await Task.Delay(200, ct);
        }
    }

    public async Task<Track?> SearchTrackAsync(string accessToken, Track sourceTrack, CancellationToken ct)
    {
        var client = CreateClient(accessToken);

        // ISRC-first search
        if (!string.IsNullOrWhiteSpace(sourceTrack.IsrcCode))
        {
            var isrcRequest = new SearchRequest(SearchRequest.Types.Track, $"isrc:{sourceTrack.IsrcCode}");
            var isrcResult = await client.Search.Item(isrcRequest);
            var isrcTrack = isrcResult.Tracks.Items?.FirstOrDefault();

            if (isrcTrack is not null)
                return MapFullTrack(isrcTrack);
        }

        // Fallback: title + artist search
        var query = $"track:{sourceTrack.Title} artist:{sourceTrack.Artist}";
        var searchRequest = new SearchRequest(SearchRequest.Types.Track, query);
        var result = await client.Search.Item(searchRequest);
        var match = result.Tracks.Items?.FirstOrDefault();

        return match is not null ? MapFullTrack(match) : null;
    }

    private static Track MapFullTrack(FullTrack ft)
    {
        string? isrc = null;
        ft.ExternalIds?.TryGetValue("isrc", out isrc);

        return new Track(
            Id: ft.Id,
            Title: ft.Name,
            Artist: string.Join(", ", ft.Artists.Select(a => a.Name)),
            Album: ft.Album?.Name,
            DurationMs: ft.DurationMs,
            IsrcCode: isrc);
    }
}
