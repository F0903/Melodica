using Discord;
using Discord.WebSocket;
using Melodica.Dependencies;

namespace Melodica.Utility;

public static class Utils
{
    public static Task<IUser> GetAppOwnerAsync() => Task.FromResult(Dependency.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner);

    public static Task<string> GetURLArgumentAsync(Span<char> url, string argName)
    {
        //Note: This algo is untested.
        var startPos = 0;
        var endPos = 0;
        for (var i = 0; i < url.Length; i++)
        {
            if (url[i] == '&')
            {
                var match = true;
                for (var j = 0; j < argName.Length; j++)
                {
                    if (url[i + j] != argName[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    startPos = i;
                    endPos = argName.Length;
                }
                else
                {
                    i += argName.Length;
                    continue;
                }
            }
        }
        return Task.FromResult(url[startPos..endPos].ToString());
    }

    public static string GetUrlResourceFormat(ReadOnlySpan<char> url)
    {
        var format = "";
        var start = url.LastIndexOf('.') + 1;
        foreach (var ch in url[start..])
        {
            if (ch is '?' or '/' or '\\') break;
            format += ch;
        }
        return format;
    }
}
