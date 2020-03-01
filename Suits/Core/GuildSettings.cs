using Discord;
using Suits.Core.Services;
using System;
using System.IO;

namespace Suits.Core
{
    [Serializable]
    public class GuildSettings
    {
        public GuildSettings(IGuild guild, IAsyncSerializer? serial = null)
        {
            serializer = serial ?? serializer;
            connectedGuild = guild;
        }

        private static IAsyncSerializer serializer = new BinarySerializer();

        private static string GetGuildSettingsPath(IGuild guild) => $"./Settings/{guild?.Id}{SettingsExtension}";

        public static GuildSettings GetOrCreateSettings(IGuild guild, Func<GuildSettings> settingsFactory)
        {
            if (!File.Exists(GetGuildSettingsPath(guild)))
            {
                var newSettings = settingsFactory();
                newSettings.SaveData();
                return newSettings;
            }
            return serializer!.DeserializeFileAsync<GuildSettings>(GetGuildSettingsPath(guild)).Result;
        }
      
        public const string DefaultPrefix = "p!";

        public const string SettingsExtension = ".ss";

        public static IGuild? connectedGuild;

        public string Prefix { get; set; } = DefaultPrefix;

        public int MaxFileCacheInMB { get; set; } = 2000;

        public void SaveData()
        {
            serializer.SerializeToFileAsync(GetGuildSettingsPath(connectedGuild!), this);
        }
    }
}