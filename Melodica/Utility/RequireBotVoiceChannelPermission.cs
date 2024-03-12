using Discord;
using Discord.Interactions;

namespace Melodica.Utility;
public class RequireBotVoiceChannelPermission(ChannelPermission permissions) : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var user = (context.User as IGuildUser) ?? throw new NullReferenceException("Guild user cast failed.");
        var userVoice = user.VoiceChannel;
        
        var bot = await context.Guild.GetCurrentUserAsync();
        var botPermissions = bot.GetPermissions(userVoice);

        if (!botPermissions.Has(permissions))
        {
            return PreconditionResult.FromError($"Bot requires voice channel permissions: {permissions}");
        }

        return PreconditionResult.FromSuccess();
    }
}
