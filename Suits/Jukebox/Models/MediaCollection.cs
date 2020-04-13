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
            
        }

        public MediaCollection(IEnumerable<PlayableMedia> playlist, MediaInfo playlistInfo, int playlistIndex = 0)
        {
            IsPlaylist = true;
            this.playlist = playlist.ToArray();
            this.Info = playlistInfo;
            
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

        public MediaInfo Info { get; }

        public bool IsPlaylist { get; }

        public int PlaylistIndex { get; }

        public string GetTitle() => Info.Title;

        public TimeSpan GetDuration() => playlist.Sum(x => x.Meta.Info.Duration);

        public string? GetThumbnail() => Info.Thumbnail;

        public string? GetID() => Info.ID;

        public string? GetURL() => IsPlaylist ? $"https://www.youtube.com/playlist?list={Info.ID}" : playlist[0].Meta.Info.URL;

        IEnumerator<PlayableMedia> IEnumerable<PlayableMedia>.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();
    }
}