using System.Collections.Concurrent;
using Discord;

namespace Melodica.Services.Playback;

public static class JukeboxManager
{
    static readonly ConcurrentDictionary<IGuild, Jukebox> jukeboxes = new();

    public static Jukebox GetJukebox(IGuild guild) => jukeboxes[guild];

    public static Jukebox GetOrCreateJukebox(IGuild guild) => jukeboxes.GetOrAdd(guild, new Jukebox());
}
