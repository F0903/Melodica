using Discord.Commands;
using Discord.WebSocket;
using CasinoBot.Utility.Extensions;
using System;
using System.Threading.Tasks;

namespace CasinoBot.CommandModules
{
    public class MiscCommandModule : ModuleBase<SocketCommandContext>
    {
        public MiscCommandModule()
        {
        }

        [Command("Spam"), RequireOwner]
        public async Task SpamAsync(int? n, string user, [Remainder] string msg)
        {
            if (n == null)
                n = 3;

            var dm = await Context.Guild.AutoGetUser(user).GetOrCreateDMChannelAsync();
            for (int i = 0; i < n; i++)
            {
                await Task.Delay(1);
                await dm.SendMessageAsync(msg);
            }
        }

        [Command("Help"), Summary("Prints out all available commands.")]
        public async Task HelpAsync()
        {
            await ReplyAsync("This command is not yet available.");
        }

        [Command("Ping")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

        [Command("Owner")]
        public async Task GetOwnerAsync()
        {
            await ReplyAsync(Context.Message.Author.IsOwnerOfApp() ? "You already know..." : $"My owner is {CasinoBot.Utility.Utility.GetAppOwnerAsync()}");
        }

        [Group("Debug"), RequireOwner(ErrorMessage = "This command group can only be used by the app owner.")]
        public class DebugCommands : ModuleBase<SocketCommandContext>
        {
            [Command("GetInstances"), Alias("Instances")]
            public Task GetNumberOfInstances(string objName)
            {
                ReplyAsync($"{CasinoBot.Utility.InstanceTester.GetNumberOfInstances(objName)} instances of {objName}");
                return Task.CompletedTask;
            }

            [Command("ThrowException"), Alias("Throw")]
            public Task ThrowExceptionAsync() =>
                throw new Exception("Test exception.");

            [Command("GetTokenType"), Alias("Token")]
            public Task GetTokenTypeAsync() =>
                ReplyAsync($"Context Client: {Context.Client.TokenType.ToString()}\nDI Client: {CasinoBot.IoC.Kernel.Get<DiscordSocketClient>().TokenType.ToString()}");
        }
    }
}