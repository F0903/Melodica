using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace PokerBot.Utility.Extensions
{
    public static class Extensions
    {
        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

        public static string RemoveSpecialCharacters(this string str)=>    
             Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.None);       
    }
}
