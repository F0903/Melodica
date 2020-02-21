using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Models
{
    public class MediaCollection : IEnumerable<PlayableMedia>, IMediaInfo
    {
        public MediaCollection(PlayableMedia media)
        {
            IsPlaylist = false;
            playlist = new[] { media };
            PlaylistName = media.GetTitle();
            PlaylistIndex = 0;
        }

        public MediaCollection(IEnumerable<PlayableMedia> playlist, string playlistName, int playlistIndex = 0)
        {
            IsPlaylist = true;
            this.playlist = playlist.ToArray();
            PlaylistName = playlistName;
            PlaylistIndex = playlistIndex;
        }

        public static implicit operator MediaCollection(PlayableMedia med) => new MediaCollection(med);

        public PlayableMedia this[int index]
        {
            get
            {
                if (index > playlist.Length)
                    throw new IndexOutOfRangeException();

                return playlist[index];
            }
        }

        private readonly PlayableMedia[] playlist;

        public int Length { get => playlist.Length; }

        public TimeSpan TotalDuration => playlist.Sum(x => x.Meta.Duration);

        public bool IsPlaylist { get; private set; }

        public string PlaylistName { get; private set; }

        public int PlaylistIndex { get; private set; }

        public string? Thumbnail => GetThumbnail();

        public string GetTitle() => PlaylistName;
        public TimeSpan GetDuration() => TotalDuration;
        public string? GetThumbnail() => playlist[0].Meta.ThumbnailUrl;

        IEnumerator<PlayableMedia> IEnumerable<PlayableMedia>.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();
    }
}