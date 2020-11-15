using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Discord;

namespace Melodica.Services.Playback
{
    public static class JukeboxFactory
    {
        private static readonly ConcurrentDictionary<IGuild, Jukebox> Jukeboxes = new ConcurrentDictionary<IGuild, Jukebox>();

        public static Task<Jukebox> GetJukeboxAsync(IGuild guild) => Task.FromResult(Jukeboxes[guild]);

        public static Task<Jukebox> GetOrCreateJukeboxAsync(IGuild guild, Func<Jukebox> newFactory)
        {
            var juke = Jukeboxes.GetOrAdd(guild, newFactory());
            return Task.FromResult(juke);
        }
    }
}