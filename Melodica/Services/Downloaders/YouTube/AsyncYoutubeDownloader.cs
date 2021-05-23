﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using AngleSharp.Text;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Melodica.Services.Downloaders.YouTube
{
    public class AsyncYoutubeDownloader : IAsyncDownloader
    {
        private static readonly Regex urlRegex = new(@"((http)|(https)):\/\/(www\.)?((youtube\.com\/((watch\?v=)|(playlist\?list=)))|(youtu\.be\/)).+", RegexOptions.Compiled);
        private readonly YoutubeClient yt = new();
        private readonly MediaFileCache cache = new("YouTube");

        async Task<TimeSpan> GetTotalDurationAsync(Playlist pl)
        {
            var videos = yt.Playlists.GetVideosAsync(pl.Id);
            var ts = new TimeSpan();
            await foreach (var video in videos)
            {
                if (video.Duration is null)
                    continue;
                ts += video.Duration.Value;
            }
            return ts;
        }

        async Task<string> GetPlaylistThumbnail(Playlist pl)
        {
            var videos = yt.Playlists.GetVideosAsync(pl.Id);
            var video = await videos.FirstAsync();
            return video.Thumbnails[0].Url;
        }


        static MediaInfo VideoToMetadata(Video video)
        {
            var (artist, title) = video.Title.AsSpan().SeperateArtistName();
            return new MediaInfo(video.Id)
            {
                Title = title,
                Artist = artist,
                Duration = video.Duration ?? TimeSpan.Zero,
                Url = video.Url,
                ImageUrl = video.Thumbnails[0].Url,
                MediaType = video.Duration != TimeSpan.Zero ? MediaType.Video : MediaType.Livestream
            };
        }

        static MediaInfo VideoToMetadata(PlaylistVideo video)
        {
            var (artist, title) = video.Title.AsSpan().SeperateArtistName();
            return new MediaInfo(video.Id)
            {
                Title = title,
                Artist = artist,
                Duration = video.Duration ?? TimeSpan.Zero,
                Url = video.Url,
                ImageUrl = video.Thumbnails[0].Url,
                MediaType = MediaType.Video
            };
        }

        async Task<MediaInfo> PlaylistToMetadataAsync(Playlist pl)
        {
            var author = pl.Author?.Title ?? "Unknown Artist";
            var (artist, newTitle) = pl.Title.AsSpan().SeperateArtistName(author);
            return new MediaInfo(pl.Id)
            {
                MediaType = MediaType.Playlist,
                Duration = await GetTotalDurationAsync(pl),
                ImageUrl = await GetPlaylistThumbnail(pl),
                Title = newTitle,
                Artist = artist,
                Url = pl.Url
            };
        }

        static bool IsUnavailable(Exception ex) =>
            ex is YoutubeExplode.Exceptions.VideoUnplayableException ||
            ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
            ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException;

        static bool IsUrlPlaylist(ReadOnlySpan<char> url) => url.Contains("playlist", StringComparison.Ordinal);

        public bool IsUrlSupported(ReadOnlySpan<char> url) => urlRegex.IsMatch(url.ToString());

        async Task<MediaInfo> GetInfoFromPlaylistUrlAsync(ReadOnlyMemory<char> url)
        {
            var id = PlaylistId.Parse(url.ToString());
            var playlist = await yt.Playlists.GetAsync(id);
            return await PlaylistToMetadataAsync(playlist);
        }

        async Task<MediaInfo> GetInfoFromUrlAsync(ReadOnlyMemory<char> url)
        {
            if (IsUrlPlaylist(url.Span))
            {
                return await GetInfoFromPlaylistUrlAsync(url);
            }

            var id = VideoId.Parse(url.ToString());
            var video = await yt.Videos.GetAsync(id);
            return VideoToMetadata(video);
        }

        public async Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
        {
            if (query.IsUrl())
            {
                return await GetInfoFromUrlAsync(query);
            }

            var videos = yt.Search.GetVideosAsync(query.ToString());
            var result = await videos.FirstAsync();
            var video = await yt.Videos.GetAsync(result.Id);
            return VideoToMetadata(video);
        }

        async Task<MediaCollection> DownloadLivestream(MediaInfo info)
        {
            var streamUrl = await yt.Videos.Streams.GetHttpLiveStreamUrlAsync(info.Id ?? throw new NullReferenceException("Id was null."));
            var media = new StreamableMedia(info, streamUrl, "hls");
            return new MediaCollection(media);
        }

        async Task<MediaCollection> DownloadPlaylist(MediaInfo info)
        {
            if (info.Id is null)
                throw new NullReferenceException("Id was null.");

            var videos = new List<LazyMedia>(10);
            await foreach (var video in yt.Playlists.GetVideosAsync(info.Id))
            {
                if (video is null)
                    continue;

                async Task<DataPair> DataGetter(PlayableMedia self)
                {
                    var manifest = await yt.Videos.Streams.GetManifestAsync(self.Info.Id);
                    var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    if (streamInfo is null)
                        throw new MediaUnavailableException("Media was unavailable.");
                    var format = streamInfo.Container.Name.ToLower();
                    Stream stream;
                    try
                    {
                        stream = await yt.Videos.Streams.GetAsync(streamInfo);
                    }
                    catch (Exception ex) when (IsUnavailable(ex))
                    {
                        throw new MediaUnavailableException("Video was unavailable.", ex);
                    }

                    return new(stream, format);
                }

                var vidInfo = VideoToMetadata(video);
                var media = new PlayableMedia(vidInfo, info, DataGetter, cache);
                videos.Add(media);
            }
            return new MediaCollection(videos, info);
        }

        public async Task<MediaCollection> DownloadAsync(MediaInfo info)
        {
            if (info.MediaType == MediaType.Playlist)
            {
                return await DownloadPlaylist(info);
            }

            if (info.MediaType == MediaType.Livestream)
            {
                return await DownloadLivestream(info);
            }

            if (info.Id is null)
                throw new NullReferenceException("Id was null.");

            async Task<DataPair> DataGetter(PlayableMedia self)
            {
                var manifest = await yt.Videos.Streams.GetManifestAsync(self.Info.Id ?? throw new NullReferenceException("Id was null"));
                var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate() ?? throw new NullReferenceException("Could not get stream from YouTube.");
                var stream = await yt.Videos.Streams.GetAsync(streamInfo);
                var format = streamInfo.Container.Name.ToLower();
                return new(stream, format);
            }

            var media = new PlayableMedia(info, null, DataGetter, cache);
            return new MediaCollection(media);
        }
    }
}