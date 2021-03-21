using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Discord;

using Melodica.Services.Media;

namespace Melodica.Services.Playback
{
    public class PlaybackEmbedHandler
    {
        public PlaybackEmbedHandler(IMessageChannel callbackChannel)
        {
            this.callbackChannel = callbackChannel;
        }

        private readonly IMessageChannel callbackChannel;
        private readonly ManualResetEventSlim mediaCallbackLock = new(true);
        private IUserMessage? lastPlayMessage;

        async ValueTask<IUserMessage> ReplyAsync(string? msg, bool tts = false, Embed? embed = null)
        {
            return await callbackChannel.SendMessageAsync(msg, tts, embed);
        }

        //TODO: make work
        public async ValueTask MediaCallback(MediaInfo info, MediaInfo? playlistInfo, MediaState state)
        {
            if (info is null)
            {
                await ReplyAsync("Info was null in MediaCallback. (dbg)");
                return;
            }

            try
            {
                mediaCallbackLock.Wait();
                mediaCallbackLock.Reset();

                if (info.MediaType == MediaType.Playlist)
                {
                    var plEmbed = PlaybackUtils.CreateMediaEmbed(info, null, MediaState.Queued);
                    await ReplyAsync(null, false, plEmbed);
                    return;
                }

                var embed = PlaybackUtils.CreateMediaEmbed(info, playlistInfo, state);

                if (state == MediaState.Downloading || state == MediaState.Queued)
                {
                    lastPlayMessage = await ReplyAsync(null, false, embed);
                }

                if (lastPlayMessage is null)
                {
                    await ReplyAsync(null, false, embed);
                    return;
                }

                await lastPlayMessage.ModifyAsync(x => x.Embed = embed);

                if (state == MediaState.Finished || state == MediaState.Error)
                {
                    lastPlayMessage = null;
                }
            }
            finally
            {
                mediaCallbackLock.Set();
            }
        }
    }
}
