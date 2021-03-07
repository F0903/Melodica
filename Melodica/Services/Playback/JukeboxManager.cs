using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Discord;

namespace Melodica.Services.Playback
{
    public static class JukeboxManager
    {
        static readonly ConcurrentDictionary<IGuild, Jukebox> jukeboxes = new();

        public static Task<Jukebox> GetJukeboxAsync(IGuild guild) => Task.FromResult(jukeboxes[guild]);

        public static Task<Jukebox> GetOrCreateJukeboxAsync(IGuild guild, Func<Jukebox> newFactory)
        {
            var juke = jukeboxes.GetOrAdd(guild, newFactory());
            return Task.FromResult(juke);
        }
    }
}