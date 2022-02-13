using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Services.Media;

namespace Melodica.Services.Playback;

public class PlayerButton
{
    private PlayerButton(string id)
    {
        this.id = id;
    }

    readonly string id;

    public static readonly PlayerButton PlayPause = "player_togglepause";
    public static readonly PlayerButton Stop = "player_stop";
    public static readonly PlayerButton Skip = "player_skip";
    public static readonly PlayerButton Shuffle = "player_shuffle";
    public static readonly PlayerButton Repeat = "player_repeat";
    public static readonly PlayerButton Loop = "player_loop";

    public static implicit operator PlayerButton(string id) => new(id);
    public static implicit operator string(PlayerButton playerButton) => playerButton.id;
}

public class Player
{
    public Player(SocketInteractionContext context)
    {
        this.context = context;
    }

    readonly SocketInteractionContext context;

    readonly MessageComponent player =
            new ComponentBuilder()
            .WithButton(customId: PlayerButton.PlayPause, emote: Emoji.Parse(":play_pause:"))
            .WithButton(customId: PlayerButton.Stop, emote: Emoji.Parse(":stop_button:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Skip, emote: Emoji.Parse(":track_next:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Shuffle, emote: Emoji.Parse(":twisted_rightwards_arrows:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Repeat, emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Loop, emote: Emoji.Parse(":repeat_one:"), style: ButtonStyle.Secondary)
            .Build();

    async Task ChangeButton(Func<IMessageComponent, bool> selector, Func<IMessageComponent, IMessageComponent> modifier)
    {
        var playerInt = (SocketMessageComponent)context.Interaction;
        var msg = playerInt.Message;
        var msgComps = msg.Components;
        await msg.ModifyAsync(x =>
        {
            var compBuilder = new ComponentBuilder();
            foreach (var row in msgComps)
            {
                var rowBuilder = new ActionRowBuilder();
                foreach (var comp in row.Components)
                {
                    var toAdd = comp;
                    if (selector(comp))
                    {
                        toAdd = modifier(comp);
                    }
                    rowBuilder.AddComponent(toAdd);
                }
                compBuilder.AddRow(rowBuilder);
            }
            x.Components = compBuilder.Build();
        });
    }

    public async Task DisableButton(PlayerButton button)
    {
        await ChangeButton(x => x.CustomId == button, x =>
        {
            return (x as ButtonComponent)!
                .ToBuilder()
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(true)
                .Build();
        });
    }

    public async Task SetButtonState(PlayerButton button, bool pressed)
    {
        await ChangeButton(x => x.CustomId == button, x =>
        {
            return (x as ButtonComponent)!
                .ToBuilder()
                .WithStyle(pressed ? ButtonStyle.Primary : ButtonStyle.Secondary)
                .Build();
        });
    }

    public async Task DisableAllButtons()
    {
        await ChangeButton(_ => true, x =>
        {
            return (x as ButtonComponent)!
                .ToBuilder()
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(true)
                .Build();
        });
    }

    public async Task Spawn(MediaInfo mediaInfo, MediaInfo? colInfo)
    {
        var interaction = context.Interaction;
        await interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Embed = EmbedUtils.CreateMediaEmbed(mediaInfo, colInfo, MediaState.Queued);
            x.Components = player;
        });
    }

    public async Task SetSongEmbed(MediaInfo info, MediaInfo? collectionInfo)
    {
        var interaction = context.Interaction;
        var embed = EmbedUtils.CreateMediaEmbed(info, collectionInfo, MediaState.Queued);
        await interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed;
        });
    }
}
