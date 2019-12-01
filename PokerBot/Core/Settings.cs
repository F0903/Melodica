using PokerBot.Filehandlers.XML;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Core
{
    public static class Settings
    {
        public static string Token { get => XmlParser.ReadContentAsync<string>("Token").Result; }

        public static string Prefix { get => XmlParser.ReadContentAsync<string>("Prefix").Result; }

        public static Discord.LogSeverity LogSeverity { get => (Discord.LogSeverity)XmlParser.ReadContentAsync<int>("LogSeverity").Result; }

        public static int MaxFileCacheInGB { get => XmlParser.ReadContentAsync<int>("MaxFileCacheInGB").Result; }

        public static bool ClearFileCacheOnStartup { get => XmlParser.ReadContentAsync<bool>("ClearFileCacheOnStartup").Result; }

        public static bool RedirectBotDMToOwner { get => XmlParser.ReadContentAsync<bool>("RedirectBotDMToOwner").Result; }
    }
}
