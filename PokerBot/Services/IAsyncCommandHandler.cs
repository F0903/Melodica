﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncCommandHandler
    {
        public Task BuildCommands(Discord.IDiscordClient client);
        public Task HandleCommands(Discord.IMessage message);
    }
}