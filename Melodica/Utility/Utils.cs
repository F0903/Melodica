using System;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

namespace Melodica.Utility
{
    public static class Utils
    {
        public static Task<IUser> GetAppOwnerAsync() =>
            Task.FromResult(IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner);

        public static Task<string> GetURLArgumentAsync(Span<char> url, string argName)
        {
            //Note: This algo is untested.
            int startPos = 0;
            int endPos = 0;
            for (int i = 0; i < url.Length; i++)
            {
                if(url[i] == '&')
                {
                    bool match = true;
                    for (int j = 0; j < argName.Length; j++)
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
            string format = "";
            int start = url.LastIndexOf('.') + 1;
            foreach (char ch in url[start..])
            {
                if (ch == '?' || ch == '/' || ch == '\\') break;
                format += ch;
            }
            return format;
        }
    }
}