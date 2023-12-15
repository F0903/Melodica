using Microsoft.Extensions.Configuration;

namespace Melodica.Config;
public sealed class BotSecrets
{
    public BotSecrets(IConfigurationRoot config)
    {
        this.config = config;
        ReadAndSetValues();
    }

    readonly IConfigurationRoot config;

    void ReadAndSetValues()
    {
        string? token;

        token = config["discordToken"];
        if (token is null)
            throw new NullReferenceException("discordToken is not defined in user secrets!");
        DiscordToken = token;

        token = config["spotifySecret"];
        if (token is null)
            throw new NullReferenceException("spotifySecret is not defined in user secrets!");
        SpotifyClientSecret = token;


        token = config["spotifyID"];
        if (token is null)
            throw new NullReferenceException("spotifyID is not defined in user secrets!");
        SpotifyClientID = token;

        token = config["geniusToken"];
        if (token is null)
            throw new NullReferenceException("geniusToken is not defined in user secrets!");
        GeniusToken = token;

        token = config["soundcloudClientID"];
        if (token is null)
            throw new NullReferenceException("soundcloudClientID is not defined in user secrets!");
        SoundcloudClientID = token;
    }

    public void Reload() => ReadAndSetValues();

    public string? DiscordToken { get; private set; }

    public string? SpotifyClientSecret { get; private set; }

    public string? SpotifyClientID { get; private set; }

    public string? GeniusToken { get; private set; }

    public string? SoundcloudClientID { get; private set; }
}
