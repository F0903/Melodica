using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CasinoBot.Utility
{
    public class Utility
    {
        public static Task<IUser> GetAppOwnerAsync() =>
            Task.FromResult(IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner);

        public static Task<int?> GetURLArgumentIntValueAsync(string url, string argName, bool throwOnNull = true)
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
    }

    public static class InstanceTester
    {
        private static readonly Dictionary<string, int> instances = new Dictionary<string, int>();

        public static void IncrementInstance(object sender)
        {
            var name = sender.GetType().Name;
            if (!instances.ContainsKey(name))
            {
                instances.Add(name, 1);
                return;
            }

            instances[name]++;
        }

        public static void DecrementInstance(object sender)
        {
            var name = sender.GetType().Name;
            if (!instances.ContainsKey(name))
                throw new Exception("Object not registered in dictionary.");
            instances[name]--;
        }

        public static int GetNumberOfInstances(string nameOfObject)
        {
            if (!instances.ContainsKey(nameOfObject))
            {
                throw new Exception("Object not registered in dictionary.");
            }
            return instances[nameOfObject];
        }
    }
}