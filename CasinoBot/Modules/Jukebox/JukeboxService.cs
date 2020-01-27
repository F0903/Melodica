using CasinoBot.Modules.Jukebox;
using Discord;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox
{
    public class JukeboxService
    {
        private readonly ConcurrentDictionary<IGuild, JukeboxPlayer> jukeboxes = new ConcurrentDictionary<IGuild, JukeboxPlayer>();

        public Task<JukeboxPlayer> GetJukeboxAsync(IGuild guild)
        {
            if (!jukeboxes.TryGetValue(guild, out var juke))
                if (!jukeboxes.TryAdd(guild, new JukeboxPlayer(new Services.Cache.MediaCache(guild))))
                    throw new System.Exception("Could not add or get player from dictionary.");

            return Task.FromResult(juke ?? jukeboxes[guild]);
        }
    }
}