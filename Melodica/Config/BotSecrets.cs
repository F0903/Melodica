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

        token = config["discordToken"] ?? throw new NullReferenceException("discordToken is not defined in user secrets!");
        DiscordToken = token;

        token = config["spotifySecret"] ?? throw new NullReferenceException("spotifySecret is not defined in user secrets!");
        SpotifyClientSecret = token;


        token = config["spotifyID"] ?? throw new NullReferenceException("spotifyID is not defined in user secrets!");
        SpotifyClientID = token;

        token = config["geniusToken"] ?? throw new NullReferenceException("geniusToken is not defined in user secrets!");
        GeniusToken = token;

        token = config["soundcloudClientID"] ?? throw new NullReferenceException("soundcloudClientID is not defined in user secrets!");
        SoundcloudClientID = token;
    }

    public void Reload() => ReadAndSetValues();

    public string? DiscordToken { get; private set; }

    public string? SpotifyClientSecret { get; private set; }

    public string? SpotifyClientID { get; private set; }

    public string? GeniusToken { get; private set; }

    public string? SoundcloudClientID { get; private set; }
}
