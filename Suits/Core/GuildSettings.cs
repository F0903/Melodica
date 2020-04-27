using Discord;
using Suits.Core.Services;
using System;
using System.IO;

namespace Suits.Core
{
    [Serializable]
    public class GuildSettings
    {
        private GuildSettings(IGuild guild, IAsyncSerializer? serial = null)
        {
            serializer = serial ?? serializer;
            connectedGuild = guild;
        }

        private static IAsyncSerializer serializer = new BinarySerializer();

        private static string GetGuildSettingsPath(IGuild guild) => $"{BotSettings.SettingsDir}{guild.Id}{SettingsExtension}";

        public static GuildSettings Get(IGuild guild)
        {
            var guildSettingsPath = GetGuildSettingsPath(guild);
            if (!File.Exists(guildSettingsPath))
            {
                return new GuildSettings(guild);
            }
            return serializer!.DeserializeFileAsync<GuildSettings>(guildSettingsPath).Result;
        }
      
        public const string DefaultPrefix = "p!";

        public const string SettingsExtension = ".ss";

        public static IGuild? connectedGuild;

        public string Prefix { get; set; } = DefaultPrefix;

        public void SaveData()
        {
            serializer.SerializeToFileAsync(GetGuildSettingsPath(connectedGuild!), this);
        }
    }
}