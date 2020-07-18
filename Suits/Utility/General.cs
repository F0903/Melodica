using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Suits.Utility
{
    public static class General
    {
        public static Task<IUser> GetAppOwnerAsync() =>
            Task.FromResult(IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner);

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