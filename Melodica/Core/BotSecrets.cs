using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Core
{
    public static class BotSecrets
    {
        public static string SpotifyClientSecret { get; } = File.ReadAllText("secrets/spotifysecret.txt");

        public static string SpotifyClientID { get; } = File.ReadAllText("secrets/spotifyid.txt");

        public static string GeniusAccessToken { get; } = File.ReadAllText("secrets/geniustoken.txt");

        public static string DiscordToken { get; } = File.ReadAllText("secrets/token.txt");
    }
}
