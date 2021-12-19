﻿
using Melodica.Core;

namespace Melodica.Services.Settings;

public static class GuildSettings
{
    public static Task<int> UpdateSettingsAsync(ulong guildId, Func<GuildSettingsInfo, GuildSettingsInfo> edit)
    {
        using GuildSettingsContext? db = new GuildSettingsContext();
        GuildSettingsInfo settings;
        try { settings = db.GuildSettings.Single(x => x.GuildID == guildId); }
        catch (InvalidOperationException)
        {
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GuildSettingsInfo>? entry = db.Add(new GuildSettingsInfo() { GuildID = guildId, Prefix = BotSettings.DefaultPrefix });
            settings = entry.Entity;
        }
        settings = edit(settings);
        return db.SaveChangesAsync();
    }

    public static Task<GuildSettingsInfo> GetSettingsAsync(ulong guildId)
    {
        using GuildSettingsContext? db = new GuildSettingsContext();
        GuildSettingsInfo settings;
        try { settings = db.GuildSettings.Single(x => x.GuildID == guildId); }
        catch (InvalidOperationException)
        {
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<GuildSettingsInfo>? entry = db.Add(new GuildSettingsInfo() { GuildID = guildId, Prefix = BotSettings.DefaultPrefix });
            db.SaveChanges();
            settings = entry.Entity;
        }
        return Task.FromResult(settings);
    }
}
