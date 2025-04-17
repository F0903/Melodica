using Discord;
using Melodica.Services.Media;
using Melodica.Utility;

namespace Melodica.Services.Playback;

public sealed class JukeboxInterfaceButton(string id, string label, IEmote emote, ButtonStyle style, bool enabled = true)
{
    internal string Id { get; set; } = id;
    internal string Label { get; set; } = label;
    internal IEmote Emote { get; set; } = emote;
    internal ButtonStyle Style { get; set; } = style;
    internal bool Enabled { get; set; } = enabled;

    public static readonly JukeboxInterfaceButton PlayPause = new("player_togglepause", "Play/Pause", Emoji.Parse(":play_pause:"), ButtonStyle.Primary);
    public static readonly JukeboxInterfaceButton Stop = new("player_stop", "Stop", Emoji.Parse(":stop_button:"), ButtonStyle.Secondary);
    public static readonly JukeboxInterfaceButton Skip = new("player_skip", "Skip", Emoji.Parse(":track_next:"), ButtonStyle.Secondary, enabled: false);
    public static readonly JukeboxInterfaceButton Shuffle = new("player_shuffle", "Shuffle", Emoji.Parse(":twisted_rightwards_arrows:"), ButtonStyle.Secondary);
    public static readonly JukeboxInterfaceButton Repeat = new("player_repeat", "Repeat", Emoji.Parse(":repeat:"), ButtonStyle.Secondary);
    public static readonly JukeboxInterfaceButton Loop = new("player_loop", "Loop", Emoji.Parse(":repeat_one:"), ButtonStyle.Secondary);

    public ButtonComponent ToComponent(ButtonBuilder builder)
    {
        return AppendBuilder(builder).Build();
    }

    public ButtonBuilder AppendBuilder(ButtonBuilder builder)
    {
        return builder
            .WithCustomId(Id)
            .WithLabel(Label)
            .WithEmote(Emote)
            .WithStyle(Style)
            .WithDisabled(!Enabled);
    }

    public override int GetHashCode() => base.GetHashCode();

    public override bool Equals(object? obj)
    {
        return
            obj is JukeboxInterfaceButton btn &&
            btn.Id == Id &&
            btn.Label == Label &&
            btn.Emote == Emote &&
            btn.Style == Style &&
            btn.Enabled == Enabled;
    }
}

public sealed class JukeboxInterface(IDiscordInteraction interaction)
{
    IUserMessage? interfaceMessage;

    readonly Dictionary<string, JukeboxInterfaceButton> buttonStates = new()
    {
        ["player_togglepause"] = JukeboxInterfaceButton.PlayPause.ShallowClone(),
        ["player_stop"] = JukeboxInterfaceButton.Stop.ShallowClone(),
        ["player_skip"] = JukeboxInterfaceButton.Skip.ShallowClone(),
        ["player_shuffle"] = JukeboxInterfaceButton.Shuffle.ShallowClone(),
        ["player_repeat"] = JukeboxInterfaceButton.Repeat.ShallowClone(),
        ["player_loop"] = JukeboxInterfaceButton.Loop.ShallowClone(),
    };

    MessageComponent BuildPlayerComponent()
    {
        var builder = new ComponentBuilder();
        foreach (var button in buttonStates.Values)
        {
            var buttonBuilder = new ButtonBuilder();
            button.AppendBuilder(buttonBuilder);
            builder.WithButton(buttonBuilder);
        }

        return builder.Build();
    }

    MessageComponent? BuildComponent(Func<IMessageComponent, (IMessageComponent, bool)> forEach)
    {
        var msgComps = interfaceMessage!.Components;
        var compBuilder = new ComponentBuilder();
        foreach (var row in msgComps)
        {
            ActionRowBuilder rowBuilder = new();
            foreach (var comp in ((ActionRowComponent)row).Components)
            {
                var (modifiedComp, abort) = forEach(comp);
                if (abort) return null;
                rowBuilder.AddComponent(modifiedComp);
            }
            compBuilder.AddRow(rowBuilder);
        };
        return compBuilder.Build();
    }

    async Task ChangeButtonAsync(string buttonId, Action<JukeboxInterfaceButton> modifier)
    {
        if (interfaceMessage is null) return;

        var component = BuildComponent(x =>
        {
            if (buttonId == x.CustomId)
            {
                var button = buttonStates[x.CustomId];
                var oldButton = button.ShallowClone();
                modifier(button);
                if (button.Equals(oldButton))
                {
                    return (x, true);
                }
                return (button.ToComponent(((ButtonComponent)x).ToBuilder()), false);
            }
            return (x, false);
        });

        if (component is null) return;

        await interfaceMessage.ModifyAsync(x =>
        {
            x.Components = component;
        });
    }

    public async Task DisableAllButtonsAsync()
    {
        if (interfaceMessage is null) return;

        var component = BuildComponent(x =>
        {
            var button = buttonStates[x.CustomId];
            button.Enabled = false;
            return (button.ToComponent(((ButtonComponent)x).ToBuilder()), false);
        });

        await interfaceMessage.ModifyAsync(x =>
        {
            x.Components = component;
        });
    }

    public async Task SetButtonPressedAsync(JukeboxInterfaceButton button, bool pressed)
    {
        await ChangeButtonAsync(button.Id, x =>
        {
            x.Style = pressed ? ButtonStyle.Primary : ButtonStyle.Secondary;
        });
    }

    public async Task SetButtonEnabledAsync(JukeboxInterfaceButton button, bool enabled)
    {
        await ChangeButtonAsync(button.Id, x =>
        {
            x.Enabled = enabled;
        });
    }

    public async Task SpawnAsync(MediaInfo mediaInfo, MediaInfo? colInfo, IReadOnlyList<JukeboxInterfaceButton>? enabledButtons = null)
    {
        interfaceMessage = await interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Embed = EmbedUtils.CreateMediaEmbed(mediaInfo, colInfo);
            x.Components = BuildPlayerComponent();
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
