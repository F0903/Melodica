using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Melodica.Utility
{
    public static class Utils
    {
        public static Task<IUser> GetAppOwnerAsync() =>
            Task.FromResult(IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner);

        public static Task<string> ParseURLToIdAsync(string url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url); // Just return, cause the url is probably already an id.
            var startIndex = url.LastIndexOf('/') + 1;
            var qIndx = url.IndexOf('?');
            var stopIndex = qIndx == -1 ? url.Length : qIndx;
            var id = url[startIndex..stopIndex];
            return Task.FromResult(id);
        }

        public static Task<int?> GetURLArgumentIntAsync(string url, string argName, bool throwOnNull = true)
        {
            if (!url.Contains($"&{argName}"))
            {
                if (!throwOnNull)
                    return Task.FromResult<int?>(null);
                throw new Exception("URL does not contain such argument.");
            }

            for (int x = url.IndexOf(argName); x < url.Length; x++)
            {
                if (url[x] != '=')
                    continue;
                x++;

                int diff = 1;
                for (int i = x; i < url.Length; i++)
                {
                    if (url[x] == '&')
                        diff = i - x;
                }

                var sub = url.Substring(x, diff);
                return Task.FromResult((int?)Convert.ToInt32(sub));
            }
            throw new Exception($"Unexpected parse of url argument '{argName}'");
        }

        public static string GetUrlResourceFormat(string url)
        {
            string format = "";
            var start = url.LastIndexOf('.') + 1;
            foreach (var ch in url[start..])
            {
                if (ch == '?' || ch == '/' || ch == '\\') break;
                format += ch;
            }
            return format;
        }
    }
}