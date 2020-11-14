using System;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core;

namespace Melodica.Services.Settings
{
    public class GuildSettingsProvider
    {
        public Task<int> UpdateSettingsAsync(ulong guildId, Func<GuildSettings, GuildSettings> edit)
        {
            using var db = new GuildSettingsContext();
            GuildSettings settings;
            try { settings = db.GuildSettings.Single(x => x.GuildID == guildId); }
            catch (InvalidOperationException)
            {
                var entry = db.Add(new GuildSettings() { GuildID = guildId, Prefix = BotSettings.DefaultPrefix });
                settings = entry.Entity;
            }
            settings = edit(settings);
            return db.SaveChangesAsync();
        }

        public Task<GuildSettings> GetSettingsAsync(ulong guildId)
        {
            using var db = new GuildSettingsContext();
            GuildSettings settings;
            try { settings = db.GuildSettings.Single(x => x.GuildID == guildId); }
            catch (InvalidOperationException)
            {
                var entry = db.Add(new GuildSettings() { GuildID = guildId, Prefix = BotSettings.DefaultPrefix });
                db.SaveChanges();
                settings = entry.Entity;
            }
            return Task.FromResult(settings);
        }
    }
}