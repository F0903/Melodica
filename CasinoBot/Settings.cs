using CasinoBot.Filehandlers.XML;

namespace CasinoBot
{
    public static class Settings
    {
        public static string Token { get => XmlParser.ReadContentAsync<string>("Token").Result; }

        public static string Prefix { get => XmlParser.ReadContentAsync<string>("Prefix").Result; }

        public static Discord.LogSeverity LogSeverity { get => (Discord.LogSeverity)XmlParser.ReadContentAsync<int>("LogSeverity").Result; }

        public static int MaxFileCacheInMB { get => XmlParser.ReadContentAsync<int>("MaxFileCacheInMB").Result; }

        public static bool ClearFileCacheOnStartup { get => XmlParser.ReadContentAsync<bool>("ClearFileCacheOnStartup").Result; }

        public static bool RedirectBotDMToOwner { get => XmlParser.ReadContentAsync<bool>("RedirectBotDMToOwner").Result; }
    }
}