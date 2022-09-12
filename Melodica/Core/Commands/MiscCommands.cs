
using Discord;
using Discord.Commands;

using Melodica.Utility;

namespace Melodica.Core.Commands;

public sealed class MiscCommands : ModuleBase<SocketCommandContext>
{
    public MiscCommands(CommandService commandService)
    {
        this.commandService = commandService;
    }

    private readonly CommandService commandService;

    private static List<CommandInfo>? cachedCommandInfo;

    [Command("Help"), Summary("Prints out all available commands.")]
    public async Task HelpAsync(int page = 0)
    {
        if (page < 0)
        {
            await ReplyAsync("Page number cannot be less than 0");
            return;
        }

        if (cachedCommandInfo == null)
        {
            IOrderedEnumerable<ModuleInfo>? modules = commandService.Modules.ToArray().OrderBy(x => x.Group);
            cachedCommandInfo = new List<CommandInfo>();
            foreach (ModuleInfo? module in modules)
                cachedCommandInfo.AddRange(module.Commands);
        }

        int maxElemsPerPage = 20;
        int totalPages = cachedCommandInfo.Count / maxElemsPerPage;

        if (page > totalPages)
        {
            await ReplyAsync($"Page number cannot be more than {totalPages}");
            return;
        }

        List<EmbedFieldBuilder>? fields = new(cachedCommandInfo.Count);
        int startIndx = page * maxElemsPerPage;
        int endIndx = (cachedCommandInfo.Count - startIndx) <= maxElemsPerPage ? cachedCommandInfo.Count : startIndx + maxElemsPerPage;
        for (int i = startIndx; i < endIndx; i++)
        {
            CommandInfo? command = cachedCommandInfo[i];
            IReadOnlyList<string>? commandAliases = command.Aliases;
            fields.Add(new EmbedFieldBuilder()
            {
                IsInline = false,
                Name = $"**{command.Name}**, { commandAliases.ToArray().SeperateStrings() }",
                Value = !string.IsNullOrEmpty(command.Summary) ? command.Summary : "No summary."
            });
        }

        await ReplyAsync(null, false, new EmbedBuilder()
        {
            Title = "Commands",
            Fields = fields,
            Footer = new EmbedFooterBuilder()
            {
                Text = $"{page}/{totalPages}"
            },
        }.Build());
    }

    [Command("Owner"), Summary("Gets the owner of the bot.")]
    public async Task GetOwnerAsync()
    {
        IUser? owner = await Utility.Utils.GetAppOwnerAsync();
        await ReplyAsync(Context.Message.Author.Id == owner.Id ? "You already know..." : $"My owner is {owner}");
    }

    [Command("Ping")]
    public Task PingAsync()
    {
        return ReplyAsync("Pong!");
    }

#if DEBUG
    [Command("Except"), RequireOwner]
    public Task ExceptAsync()
    {
        throw new Exception("Test exception.");
    }
#endif
}
