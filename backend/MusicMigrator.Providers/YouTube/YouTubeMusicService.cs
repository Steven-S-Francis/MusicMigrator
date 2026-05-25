using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using MusicMigrator.Core.Interfaces;
using MusicMigrator.Core.Models;
using Playlist = MusicMigrator.Core.Models.Playlist;
using Track = MusicMigrator.Core.Models.Track;

namespace MusicMigrator.Providers.YouTube;

public class YouTubeMusicService : IMusicProvider
{
    public string ProviderName => "youtube";

    private static YouTubeService CreateService(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "MusicMigrator"
        });
    }

    public async Task<IEnumerable<Playlist>> GetPlaylistsAsync(string accessToken, CancellationToken ct)
    {
        var service = CreateService(accessToken);
        var playlists = new List<Playlist>();

        try
        {
            var request = service.Playlists.List("snippet,contentDetails");
            request.Mine = true;
            request.MaxResults = 50;

            string? nextPageToken = null;

            do
            {
                request.PageToken = nextPageToken;
                var response = await request.ExecuteAsync(ct);

                foreach (var item in response.Items)
                {
                    playlists.Add(new Playlist(
                        Id: item.Id ?? string.Empty,
                        Name: item.Snippet?.Title ?? string.Empty,
                        Description: item.Snippet?.Description,
                        TrackCount: (int)(item.ContentDetails?.ItemCount ?? 0),
                        CoverUrl: item.Snippet?.Thumbnails?.Default__?.Url));
                }

                nextPageToken = response.NextPageToken;
            } while (nextPageToken is not null);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound &&
            ex.Message.Contains("Channel not found", StringComparison.OrdinalIgnoreCase))
        {
            // User does not have a YouTube channel — return empty playlists
        }

        return playlists;
    }

    public async Task<IEnumerable<Track>> GetTracksAsync(string accessToken, string playlistId, CancellationToken ct)
    {
        var service = CreateService(accessToken);

        // Step 1: collect all video IDs from the playlist
        var playlistItemsRequest = service.PlaylistItems.List("contentDetails");
        playlistItemsRequest.PlaylistId = playlistId;
        playlistItemsRequest.MaxResults = 50;

        var videoIds = new List<string>();
        string? nextPageToken = null;

        do
        {
            playlistItemsRequest.PageToken = nextPageToken;
            var itemsResponse = await playlistItemsRequest.ExecuteAsync(ct);

            foreach (var item in itemsResponse.Items)
            {
                if (item.ContentDetails?.VideoId is not null)
                    videoIds.Add(item.ContentDetails.VideoId);
            }

            nextPageToken = itemsResponse.NextPageToken;
        } while (nextPageToken is not null);

        if (videoIds.Count == 0)
            return [];

        // Step 2: batch video ID lookup in groups of 50
        var tracks = new List<Track>();

        foreach (var batch in videoIds.Chunk(50))
        {
            var videosRequest = service.Videos.List("snippet,contentDetails");
            videosRequest.Id = batch;
            var videosResponse = await videosRequest.ExecuteAsync(ct);

            foreach (var video in videosResponse.Items)
            {
                var durationMs = 0;
                if (video.ContentDetails?.Duration is not null)
                {
                    try
                    {
                        durationMs = (int)System.Xml.XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                    }
                    catch
                    {
                        durationMs = 0;
                    }
                }

                tracks.Add(new Track(
                    Id: video.Id ?? string.Empty,
                    Title: video.Snippet?.Title ?? string.Empty,
                    Artist: video.Snippet?.ChannelTitle ?? string.Empty,
                    Album: null,
                    DurationMs: durationMs,
                    IsrcCode: null));
            }
        }

        return tracks;
    }

    public async Task<string> CreatePlaylistAsync(string accessToken, string name, string? description, CancellationToken ct)
    {
        var service = CreateService(accessToken);

        var playlist = new Google.Apis.YouTube.v3.Data.Playlist
        {
            Snippet = new PlaylistSnippet
            {
                Title = name,
                Description = description ?? string.Empty
            },
            Status = new PlaylistStatus
            {
                PrivacyStatus = "private"
            }
        };

        var request = service.Playlists.Insert(playlist, "snippet,status");
        var result = await request.ExecuteAsync(ct);

        return result.Id ?? throw new InvalidOperationException("YouTube did not return a playlist ID.");
    }

    public async Task AddTracksAsync(string accessToken, string playlistId, IEnumerable<Track> tracks, CancellationToken ct)
    {
        var service = CreateService(accessToken);

        foreach (var track in tracks)
        {
            var playlistItem = new PlaylistItem
            {
                Snippet = new PlaylistItemSnippet
                {
                    PlaylistId = playlistId,
                    ResourceId = new ResourceId
                    {
                        Kind = "youtube#video",
                        VideoId = track.Id
                    }
                }
            };

            var request = service.PlaylistItems.Insert(playlistItem, "snippet");
            await request.ExecuteAsync(ct);
            await Task.Delay(100, ct);
        }
    }

    public async Task<Track?> SearchTrackAsync(string accessToken, Track sourceTrack, CancellationToken ct)
    {
        var service = CreateService(accessToken);

        var query = $"{sourceTrack.Title} {sourceTrack.Artist} official audio";
        var request = service.Search.List("snippet");
        request.Q = query;
        request.Type = "video";
        request.VideoCategoryId = "10";
        request.MaxResults = 5;

        var response = await request.ExecuteAsync(ct);
        var match = response.Items?.FirstOrDefault();

        if (match is null)
            return null;

        var durationMs = 0;

        return new Track(
            Id: match.Id?.VideoId ?? string.Empty,
            Title: match.Snippet?.Title ?? string.Empty,
            Artist: match.Snippet?.ChannelTitle ?? string.Empty,
            Album: null,
            DurationMs: durationMs,
            IsrcCode: null);
    }
}
