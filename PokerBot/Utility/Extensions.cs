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
        public static bool CheckForUser(this SocketGuild guild, string user) =>
            AutoGetUser(guild, user) != null;

        public static SocketGuildUser AutoGetUser(this SocketGuild guild, string user) =>
            guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);

        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

        public static string RemoveSpecialCharacters(this string str) =>
             Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
    }
}
