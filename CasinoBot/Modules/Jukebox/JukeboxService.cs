using CasinoBot.Modules.Jukebox;
using Discord;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox
{
    public class JukeboxService //TODO: Possibly combine with JukeboxDictionary
    {
        private readonly JukeboxDictionary<IGuild, JukeboxPlayer> jukeboxes = new JukeboxDictionary<IGuild, JukeboxPlayer>();

        public Task<JukeboxPlayer> GetJukeboxAsync(IGuild guild) 
        {
            if (!jukeboxes.TryGetEntry(guild, out var _))
                jukeboxes.AddEntry(guild, new JukeboxPlayer(guild, new Services.Cache.AsyncMediaFileCache()));

            return Task.FromResult(jukeboxes[guild]); 
        }
    }
}