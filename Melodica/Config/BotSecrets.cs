using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Melodica.Config;
public sealed class BotSecrets
{
    //REASON: Redundant warning.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public BotSecrets(IConfigurationRoot config)
    {
        this.config = config;
        ReadAndSetValues();
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    readonly IConfigurationRoot config;

    void ReadAndSetValues()
    {
        if ((DiscordToken = config["discordToken"]) is null)
            throw new NullReferenceException("discordToken is not defined in user secrets!");

        if ((SpotifyClientSecret = config["spotifySecret"]) is null)
            throw new NullReferenceException("spotifySecret is not defined in user secrets!");
        if ((SpotifyClientID = config["spotifyID"]) is null)
            throw new NullReferenceException("spotifyID is not defined in user secrets!");

        if ((GeniusToken = config["geniusToken"]) is null)
            throw new NullReferenceException("geniusToken is not defined in user secrets!");
    }

    public void Reload()
    {
        ReadAndSetValues();
    }

    public string DiscordToken { get; private set; }

    public string SpotifyClientSecret { get; private set; }

    public string SpotifyClientID { get; private set; }

    public string GeniusToken { get; private set; }
}
