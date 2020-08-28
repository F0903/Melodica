using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Discord;

namespace Melodica.Services.Playback
{
    public class JukeboxProvider
    {
        private static readonly ConcurrentDictionary<IGuild, Jukebox> Jukeboxes = new ConcurrentDictionary<IGuild, Jukebox>();

        public Task<Jukebox> GetJukeboxAsync(IGuild guild, bool throwIfNotExist = false)
        {
            if (!Jukeboxes.TryGetValue(guild, out var juke))
                if (throwIfNotExist) throw new Exception("Jukebox did not exist in dictionary. (exception requested)");
                else if (!Jukeboxes.TryAdd(guild, new Jukebox()))
                    throw new Exception("Could not add or get Jukebox from dictionary.");

            return Task.FromResult(juke ?? Jukeboxes[guild]);
        }
    }
}