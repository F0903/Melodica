using Melodica.Services.Jukebox;
using Discord;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Melodica.Services.Jukebox
{
    public class JukeboxManager
    {
        private static readonly ConcurrentDictionary<IGuild, Jukebox> Jukeboxes = new ConcurrentDictionary<IGuild, Jukebox>();

        public static Task<Jukebox> GetJukeboxAsync(IGuild guild)
        {
            if (!Jukeboxes.TryGetValue(guild, out var juke))
                if (!Jukeboxes.TryAdd(guild, new Jukebox()))
                    throw new System.Exception("Could not add or get Services.Jukebox from dictionary.");

            return Task.FromResult(juke ?? Jukeboxes[guild]);
        }
    }
}