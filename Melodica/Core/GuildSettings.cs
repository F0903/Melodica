using Discord;
using Discord.Rest;
using Melodica.Services;
using Melodica.Services.Serialization;
using System;
using System.IO;

namespace Melodica.Core
{
    [Serializable]
    public class GuildSettings
    {
        private GuildSettings(IGuild guild)
        {
            connectedGuild = guild.Id;
        }

        public const string DefaultPrefix = "m.";

        public static IAsyncSerializer Serializer { get; set; } = new BinarySerializer();

        public string Prefix { get; set; } = DefaultPrefix;

        private readonly ulong connectedGuild;

        private static string GetGuildSettingsPath(ulong guildId) => BotSettings.SettingsDir + guildId.ToString() + BotSettings.SettingsExtension;

        public static GuildSettings Get(IGuild guild)
        {
            var guildSettingsPath = GetGuildSettingsPath(guild.Id);

            if (!File.Exists(guildSettingsPath))
            {
                return new GuildSettings(guild);
            }
            return Serializer!.DeserializeFileAsync<GuildSettings>(guildSettingsPath).Result;
        }

        public void SaveData()
        {
            Serializer.SerializeToFileAsync(GetGuildSettingsPath(connectedGuild!), this);
        }
    }
}