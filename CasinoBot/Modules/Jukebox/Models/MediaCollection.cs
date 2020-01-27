using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CasinoBot.Utility.Extensions;

namespace CasinoBot.Modules.Jukebox.Models
{
    public class MediaCollection : IEnumerable<PlayableMedia>, IMediaInfo
    {       
        public MediaCollection(PlayableMedia media)
        {
            IsPlaylist = false;
            playlist = new[] { media };
        }

        public MediaCollection(IEnumerable<PlayableMedia> playlist, string playlistName, int playlistIndex = 1)
        {
            if (playlistIndex <= 0) playlistIndex = 1;

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

        public TimeSpan TotalDuration { get => playlist.Sum(x => x.Meta.Duration); }

        public bool IsPlaylist { get; private set; }

        public string PlaylistName { get; private set; }

        public int PlaylistIndex { get; private set; } = 1;

        public string GetTitle() => PlaylistName;
        public TimeSpan GetDuration() => TotalDuration;

        IEnumerator<PlayableMedia> IEnumerable<PlayableMedia>.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();       
    }
}