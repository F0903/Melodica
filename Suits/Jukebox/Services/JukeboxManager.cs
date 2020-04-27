using Suits.Jukebox;
using Discord;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Suits.Jukebox.Services
{
    public class JukeboxManager
    {
        private static readonly ConcurrentDictionary<IGuild, Jukebox> jukeboxes = new ConcurrentDictionary<IGuild, Jukebox>();

        public static Task<Jukebox> GetJukeboxAsync(IGuild guild)
        {
            if (!jukeboxes.TryGetValue(guild, out var juke))
                if (!jukeboxes.TryAdd(guild, new Jukebox()))
                    throw new System.Exception("Could not add or get jukebox from dictionary.");

            return Task.FromResult(juke ?? jukeboxes[guild]);
        }
    }
}