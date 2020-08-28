using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Lyrics
{
    public struct LyricsInfo
    {
        public string Title { get; set; }

        public string Image { get; set; }

        public string Lyrics { get; set; }
    }


    public abstract class LyricsProvider
    {
        public abstract Task<LyricsInfo> GetLyricsAsync(string input);
    }
}
