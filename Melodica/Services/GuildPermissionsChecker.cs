using System;
using System.Collections.Generic;

using Discord;
using Discord.WebSocket;

namespace Melodica.Services
{
    public static class GuildPermissionsChecker
    {
        static IReadOnlyCollection<SocketRole>? botGuildRoles;
        static OverwritePermissions? botPerms;

        /// <summary>
        /// Throws exception if bot does not have enough permissions.
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="bot"></param>
        /// <param name="voice"></param>
        public static void AssertVoicePermissions(SocketGuild guild, ISelfUser bot, IVoiceChannel voice)
        {
            botGuildRoles ??= guild.GetUser(bot.Id).Roles;
            foreach (var role in botGuildRoles) // Check through all roles.
            {
                if (role.Permissions.Administrator)
                    return;
                var guildRolePerms = voice.GetPermissionOverwrite(role);
                if (guildRolePerms == null)
                    continue;
                var allowedGuildPerms = guildRolePerms!.Value.ToAllowList();
                if (allowedGuildPerms.Contains(ChannelPermission.Connect) && allowedGuildPerms.Contains(ChannelPermission.Speak))
                    return;
            }

            // Check for user role.
            botPerms ??= voice.GetPermissionOverwrite(bot);
            if (botPerms != null)
            {
                var allowedBotPerms = botPerms!.Value.ToAllowList();
                if (allowedBotPerms.Contains(ChannelPermission.Connect) && allowedBotPerms.Contains(ChannelPermission.Speak))
                    return;
            }
            throw new Exception("I don't have explicit permission to connect and speak in this channel :(");
        }
    }
}