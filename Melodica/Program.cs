using System.Diagnostics;

using Melodica.Core;
using Melodica.Core.CommandHandlers;
using Melodica.Dependencies;
using Melodica.Services.Logging;

var bot = new SocketBot(Dependency.Get<IAsyncLogger>());
await bot.ConnectAsync($"{BotSettings.TextCommandPrefix}play | {BotSettings.TextCommandPrefix}help", Discord.ActivityType.Listening, true);

await Task.Delay(-1);
