using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace PokerBot.Utility.Extensions
{
    public static class Extensions
    {
        public static void ForEach<T>(this T[] array, Action<int, T> action)
        {
            for (int i = 0; i < array.Length; i++)
            {
                action(i + 1, array[i]);
            }
        }

        public static bool CheckForUser(this SocketGuild guild, string user) =>
            AutoGetUser(guild, user) != null;

        public static SocketGuildUser AutoGetUser(this SocketGuild guild, string user) =>
            guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);

        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

        public static string RemoveSpecialCharacters(this string str) =>
             Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);

        public static bool IsUrl(this string str) =>
             Uri.TryCreate(str, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps);

    }
}
