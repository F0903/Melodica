﻿using System.Threading.Tasks;

namespace Melodica.Services.Logging
{
    public interface IAsyncLogger
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}