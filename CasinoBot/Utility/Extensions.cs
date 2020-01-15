using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CasinoBot.Utility.Extensions
{
    public static class Extensions
    {
        public static char[] CustomIllegalChars { get; set; } =
        {
            '.'
        };

        public static IEnumerable<R> Convert<T, R>(this IEnumerable<T> col, Func<T, R> body)
        {
            foreach (var elem in col)
                yield return body(elem);
        }

        public static bool CheckForUser(this SocketGuild guild, string user) =>
            AutoGetUser(guild, user) != null;

        public static SocketGuildUser AutoGetUser(this SocketGuild guild, string user) =>
            guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);

        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

        public static string ReplaceIllegalCharacters(this string str, string replacement = "_") =>
             System.IO.Path.GetInvalidFileNameChars().Union(CustomIllegalChars).Aggregate(str, (current, c) => current.Replace(c.ToString(), replacement));

        public static bool IsUrl(this string str) =>
             Uri.TryCreate(str, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps);
    }
}