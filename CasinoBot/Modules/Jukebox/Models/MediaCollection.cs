using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CasinoBot.Modules.Jukebox.Models
{
    public class MediaCollection : IEnumerable<PlayableMedia>
    {
        public MediaCollection(string name, string path, string format, int lengthInSec)
        {
            IsPlaylist = false;
            playlist = new[] { new PlayableMedia(name, path, format, lengthInSec) };
        }
        
        public MediaCollection(PlayableMedia media)
        {
            IsPlaylist = false;
            playlist = new[] { media };
        }

        public MediaCollection(PlayableMedia[] videos, string playlistName, int playlistIndex = 1)
        {
            if (playlistIndex <= 0)
                throw new IndexOutOfRangeException("Index of playlist cannot be under 1");

            IsPlaylist = true;
            this.playlist = videos;
            PlaylistName = playlistName;
            PlaylistIndex = playlistIndex;
        }

        public PlayableMedia this[int index]
        {
            get
            {
                if (index > playlist.Length)
                    throw new IndexOutOfRangeException();

                return playlist[index];
            }
        }

        private readonly PlayableMedia[] playlist = null;

        public int Length { get => playlist.Length; }

        public int TotalDuration { get => playlist.Sum(x => x.SecondDuration); }

        public bool IsPlaylist { get; private set; }

        public string PlaylistName { get; private set; }

        public int PlaylistIndex { get; private set; } = 1;

        public PlayableMedia[] GetMedia() => playlist;

        IEnumerator<PlayableMedia> IEnumerable<PlayableMedia>.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();
    }
}