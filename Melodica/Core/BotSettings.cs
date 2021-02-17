using System.Diagnostics;
using System.IO;

using Discord;

namespace Melodica.Core
{
    public static class BotSettings
    {
        //TODO: Move these "secret" to dedicated class?
        private static string? cachedSpotifyClientSecret;
        public static string SpotifyClientSecret => cachedSpotifyClientSecret ??= File.ReadAllText("spotifysecret.txt");

        private static string? cachedSpotifyClientID;
        public static string SpotifyClientID => cachedSpotifyClientID ??= File.ReadAllText("spotifyid.txt");


        private static string? cachedGeniusAccessToken;
        public static string GeniusAccessToken => cachedGeniusAccessToken ??= File.ReadAllText("geniustoken.txt");


        private static string? cachedDiscordToken;
        public static string DiscordToken => cachedDiscordToken ??= File.ReadAllText("token.txt");

        public const string DefaultPrefix = "m.";

        public const int CacheSizeMB = 500;

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

        public const LogSeverity LogLevel = LogSeverity.Debug;
    }
}