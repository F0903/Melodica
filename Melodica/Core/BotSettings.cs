using System.Diagnostics;
using System.IO;

using Discord;

namespace Melodica.Core
{
    public static class BotSecrets
    {
        public static string SpotifyClientSecret { get; } = File.ReadAllText("spotifysecret.txt");

        public static string SpotifyClientID { get; } = File.ReadAllText("spotifyid.txt");

        public static string GeniusAccessToken { get; } = File.ReadAllText("geniustoken.txt");

        public static string DiscordToken { get; } = File.ReadAllText("token.txt");
    }

    public static class BotSettings
    {
        public const string DefaultPrefix = "m.";

        public const int CacheSizeMB = 500;

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

        public const LogSeverity LogLevel = LogSeverity.Debug;
    }
}