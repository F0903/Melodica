using Suits.Jukebox;
using Discord;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Suits.Jukebox
{
    public static class JukeboxService
    {
        private static readonly ConcurrentDictionary<IGuild, JukeboxPlayer> jukeboxes = new ConcurrentDictionary<IGuild, JukeboxPlayer>();

        public static Task<JukeboxPlayer> GetJukeboxAsync(IGuild guild)
        {
            if (!jukeboxes.TryGetValue(guild, out var juke))
                if (!jukeboxes.TryAdd(guild, new JukeboxPlayer()))
                    throw new System.Exception("Could not add or get player from dictionary.");

            return Task.FromResult(juke ?? jukeboxes[guild]);
        }
    }
}