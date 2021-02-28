using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Downloaders.Spotify
{
    [Serializable]
    public record SpotifyMediaInfo : MediaInfo
    {
        public override string? Id { get; init; }
    }
}
