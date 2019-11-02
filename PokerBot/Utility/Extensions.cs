using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;

namespace PokerBot.Utility.Extensions
{
    public static class Extensions
    {
        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;      
    }
}
