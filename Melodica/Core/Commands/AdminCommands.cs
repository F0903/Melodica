using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Melodica.Services.Settings;

namespace Melodica.Core.Commands
{
    [Group("Admin"), RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "This command can only be used by guild admins.")]
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        [Command("Prefix"), Summary("Changes the prefix.")]
        public async Task ChangePrefixAsync(string newPrefix)
        {
            int status = await GuildSettings.UpdateSettingsAsync(Context.Guild.Id, x =>
            {
                x.Prefix = newPrefix;
                return x;
            });
            if (status == 1)
            {
                await ReplyAsync(null, false, new EmbedBuilder()
                {
                    Description = $"Succesfully changed prefix in **{Context.Guild.Name}** to {newPrefix}",
                    Color = Color.Green
                }.Build());
            }
            else
            {
                await ReplyAsync(null, false, new EmbedBuilder()
                {
                    Description = $"Could not change prefix",
                    Color = Color.Red
                }.Build());
            }
        }
    }
}