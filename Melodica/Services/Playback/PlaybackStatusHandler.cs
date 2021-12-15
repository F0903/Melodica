using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;

using Melodica.Services.Media;

namespace Melodica.Services.Playback
{
    public class PlaybackStatusHandler
    {
        public PlaybackStatusHandler(IMessageChannel callbackChannel)
        {
            this.callbackChannel = callbackChannel;
        }

        private readonly ManualResetEventSlim locker = new(true);
        private readonly IMessageChannel callbackChannel;
        private IUserMessage? lastMessage;

        async ValueTask SendState(Embed embed)
        {
            locker.Wait();
            var msg = await callbackChannel.SendMessageAsync(null, false, embed);
            lastMessage = msg;
            locker.Set();
        }

        public async ValueTask Send(MediaInfo info, MediaInfo? collectionInfo, MediaState state)
        {
            var embed = EmbedUtils.CreateMediaEmbed(info, collectionInfo, state);
            await SendState(embed);
        }

        public async ValueTask SetQueued()
        {
            if (lastMessage is null) return;
            var embed = lastMessage.Embeds.First();
            await lastMessage.ModifyAsync(x => x.Embed = embed.SetEmbedToState(MediaState.Queued));
        }

        public async ValueTask SetDownloading()
        {
            if (lastMessage is null) return;
            var embed = lastMessage.Embeds.First();
            await lastMessage.ModifyAsync(x => x.Embed = embed.SetEmbedToState(MediaState.Downloading));
        }

        public async ValueTask SetPlaying()
        {
            if (lastMessage is null) return;
            var embed = lastMessage.Embeds.First();
            await lastMessage.ModifyAsync(x => x.Embed = embed.SetEmbedToState(MediaState.Playing));
        }

        public async ValueTask SetFinished()
        {
            if (lastMessage is null) return;
            var embed = lastMessage.Embeds.First();
            await lastMessage.ModifyAsync(x => x.Embed = embed.SetEmbedToState(MediaState.Finished));
        }

        public async ValueTask SetError()
        {
            if (lastMessage is null) return;
            var embed = lastMessage.Embeds.First();
            await lastMessage.ModifyAsync(x => x.Embed = embed.SetEmbedToState(MediaState.Error));
        }

        public async ValueTask RaiseError(string message)
        {
            var embed = new EmbedBuilder()
                .WithColor(EmbedUtils.MediaStateToColor(MediaState.Error))
                .WithTitle("Error!")
                .WithDescription(message)
                .Build();
            await SendState(embed);
        }
    }
}
