using Discord;

using Melodica.Services.Media;

namespace Melodica.Services.Playback;

public sealed class JukeboxInterfaceButton
{
    private JukeboxInterfaceButton(string id) => this.id = id;

    readonly string id;

    public static readonly JukeboxInterfaceButton PlayPause = "player_togglepause";
    public static readonly JukeboxInterfaceButton Stop = "player_stop";
    public static readonly JukeboxInterfaceButton Skip = "player_skip";
    public static readonly JukeboxInterfaceButton Shuffle = "player_shuffle";
    public static readonly JukeboxInterfaceButton Repeat = "player_repeat";
    public static readonly JukeboxInterfaceButton Loop = "player_loop";

    public static implicit operator JukeboxInterfaceButton(string id) => new(id);
    public static implicit operator string(JukeboxInterfaceButton playerButton) => playerButton.id;
}

public sealed class JukeboxInterface
{
    public JukeboxInterface(IDiscordInteraction interaction) => this.interaction = interaction;

    readonly IDiscordInteraction interaction;
    IUserMessage? interfaceMessage;

    static readonly MessageComponent playerComponent =
            new ComponentBuilder()
            .WithButton(customId: JukeboxInterfaceButton.PlayPause, emote: Emoji.Parse(":play_pause:"))
            .WithButton(customId: JukeboxInterfaceButton.Stop, emote: Emoji.Parse(":stop_button:"), style: ButtonStyle.Secondary)
            .WithButton(customId: JukeboxInterfaceButton.Skip, emote: Emoji.Parse(":track_next:"), style: ButtonStyle.Secondary)
            .WithButton(customId: JukeboxInterfaceButton.Shuffle, emote: Emoji.Parse(":twisted_rightwards_arrows:"), style: ButtonStyle.Secondary)
            .WithButton(customId: JukeboxInterfaceButton.Repeat, emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Secondary)
            .WithButton(customId: JukeboxInterfaceButton.Loop, emote: Emoji.Parse(":repeat_one:"), style: ButtonStyle.Secondary)
            .Build();

    public bool AreButtonsDisabled { get; private set; }

    async Task ChangeButtonAsync(Func<IMessageComponent, bool> selector, Func<IMessageComponent, IMessageComponent> modifier)
    {
        if (interfaceMessage is null) return;
        var msgComps = interfaceMessage.Components;
        await interfaceMessage.ModifyAsync(x =>
        {
            ComponentBuilder compBuilder = new();
            foreach (var row in msgComps)
            {
                ActionRowBuilder rowBuilder = new();
                foreach (var comp in ((ActionRowComponent)row).Components)
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

    public async Task SetButtonStateAsync(JukeboxInterfaceButton button, bool pressed)
    {
        await ChangeButtonAsync(x => x.CustomId == button, x =>
        {
            return ((ButtonComponent)x)
                .ToBuilder()
                .WithStyle(pressed ? ButtonStyle.Primary : ButtonStyle.Secondary)
                .Build();
        });
    }

    public async Task DisableAllButtonsAsync()
    {
        if (AreButtonsDisabled) return;
        await ChangeButtonAsync(_ => true, x =>
        {
            return ((ButtonComponent)x)
                .ToBuilder()
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(true)
                .Build();
        });
        AreButtonsDisabled = true;
    }

    public async Task SpawnAsync(MediaInfo mediaInfo, MediaInfo? colInfo)
    {
        interfaceMessage = await interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Embed = EmbedUtils.CreateMediaEmbed(mediaInfo, colInfo);
            x.Components = playerComponent;
        });
    }

    public async Task SetSongEmbedAsync(MediaInfo info, MediaInfo? collectionInfo)
    {
        if (interfaceMessage is null) return;
        var embed = EmbedUtils.CreateMediaEmbed(info, collectionInfo);
        await interfaceMessage.ModifyAsync(x =>
        {
            x.Embed = embed;
        });
    }
}
