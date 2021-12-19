namespace Melodica.Core;

public static class BotSecrets
{
    static BotSecrets()
    {
        try { DiscordToken = File.ReadAllText("secrets/token.txt"); }
        catch (Exception ex) { throw new Exception("Discord token file could not be read. Remember to create a 'secrets' folder with token.txt", ex); }
        try { SpotifyClientSecret = File.ReadAllText("secrets/spotifysecret.txt"); }
        catch (Exception ex) { throw new Exception("Spotify secret file could not be read. Remember to create a 'secrets' folder with spotifysecret.txt", ex); }
        try { SpotifyClientID = File.ReadAllText("secrets/spotifyid.txt"); }
        catch (Exception ex) { throw new Exception("Spotify ID file could not be read. Remember to create a 'secrets' folder with spotifyid.txt", ex); }
        try { GeniusAccessToken = File.ReadAllText("secrets/geniustoken.txt"); }
        catch (Exception ex) { throw new Exception("Genius token file could not be read. Remember to create a 'secrets' folder with geniustoken.txt", ex); }

    }

    public static string SpotifyClientSecret { get; }

    public static string SpotifyClientID { get; }

    public static string GeniusAccessToken { get; }

    public static string DiscordToken { get; }
}
