using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace PokerBot.Utility
{
    public class Utility
    {
        public static Task<IUser> GetAppOwnerAsync() =>
            Task.FromResult(IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner);           
    }
}
