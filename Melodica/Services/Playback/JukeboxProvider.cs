using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Discord;

namespace Melodica.Services.Playback
{
    public class JukeboxProvider
    {
        private static readonly ConcurrentDictionary<IGuild, Jukebox> Jukeboxes = new ConcurrentDictionary<IGuild, Jukebox>();

        public Task<Jukebox> GetJukeboxAsync(IGuild guild) => Task.FromResult(Jukeboxes[guild]);
        
        public Task<Jukebox> GetOrCreateJukeboxAsync(IGuild guild, Func<Jukebox> newFactory)
        {
            var juke = Jukeboxes.GetOrAdd(guild, newFactory());
            return Task.FromResult(juke);
        }
    }
}