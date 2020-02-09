using Suits.Core.Filehandlers.XML;

namespace Suits
{
    //TODO: Rewrite this to be per guild based
    public static class Settings
    {
        public static string Token { get => XmlParser.ReadContentAsync<string>("Token").Result; }

        public static string Prefix { get => XmlParser.ReadContentAsync<string>("Prefix").Result; }

        public static Discord.LogSeverity LogSeverity { get => (Discord.LogSeverity)XmlParser.ReadContentAsync<int>("LogSeverity").Result; }

        public static int MaxFileCacheInMB { get => XmlParser.ReadContentAsync<int>("MaxFileCacheInMB").Result; }

        public static bool ClearFileCacheOnStartup { get => XmlParser.ReadContentAsync<bool>("ClearFileCacheOnStartup").Result; }
    }
}