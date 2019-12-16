using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PokerBot.Models
{
    public class MediaCollection : IEnumerable<PlayableMedia>
    {
        public MediaCollection(string name, string path, string format)
        {
            IsPlaylist = false;
            playlist = new[] { new PlayableMedia(name, path, format) };
        }

        public MediaCollection(PlayableMedia media)
        {
            IsPlaylist = false;
            playlist = new[] { media };
        }

        public MediaCollection(PlayableMedia[] videos, string playlistName, int playlistIndex = 0)
        {
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

        readonly PlayableMedia[] playlist = null;

        public int Length { get => playlist.Length; }

        public bool IsPlaylist { get; private set; }

        public string PlaylistName { get; private set; }

        public int PlaylistIndex { get; private set; }

        public PlayableMedia[] GetMedia() => playlist;

        IEnumerator<PlayableMedia> IEnumerable<PlayableMedia>.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<PlayableMedia>)playlist).GetEnumerator();
    }
}
