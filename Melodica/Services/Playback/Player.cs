﻿using Discord;
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
    public Player(IInteractionContext context)
    {
        this.context = context;
    }

    IInteractionContext context;

    readonly MessageComponent player =
            new ComponentBuilder()
            .WithButton(customId: PlayerButton.PlayPause, emote: Emoji.Parse(":play_pause:"))
            .WithButton(customId: PlayerButton.Stop, emote: Emoji.Parse(":stop_button:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Skip, emote: Emoji.Parse(":track_next:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Shuffle, emote: Emoji.Parse(":twisted_rightwards_arrows:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Repeat, emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Secondary)
            .WithButton(customId: PlayerButton.Loop, emote: Emoji.Parse(":repeat_one:"), style: ButtonStyle.Secondary)
            .Build();


    async Task ChangeButtonAsync(Func<IMessageComponent, bool> selector, Func<IMessageComponent, IMessageComponent> modifier)
    {
        var interaction = context.Interaction;
        IUserMessage msg = interaction switch
        {
            SocketMessageComponent cmp => cmp.Message,
            SocketSlashCommand cmd => await cmd.GetOriginalResponseAsync(),
            _ => throw new Exception("Unknown interaction type. Contact developer.")
        };
        var msgComps = msg.Components;
        await msg.ModifyAsync(x =>
        {
            var compBuilder = new ComponentBuilder();
            foreach (var row in msgComps)
            {
                var rowBuilder = new ActionRowBuilder();
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

    public async Task DisableButtonAsync(PlayerButton button)
    {
        await ChangeButtonAsync(x => x.CustomId == button, x =>
        {
            return ((ButtonComponent)x)
                .ToBuilder()
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(true)
                .Build();
        });
    }

    public async Task SetButtonStateAsync(PlayerButton button, bool pressed)
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
        await ChangeButtonAsync(_ => true, x =>
        {
            return ((ButtonComponent)x)
                .ToBuilder()
                .WithStyle(ButtonStyle.Secondary)
                .WithDisabled(true)
                .Build();
        });
    }

    public async Task SpawnAsync(MediaInfo mediaInfo, MediaInfo? colInfo)
    {
        var interaction = context.Interaction;
        await interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Embed = EmbedUtils.CreateMediaEmbed(mediaInfo, colInfo);
            x.Components = player;
        });
    }

    public async Task SetSongEmbedAsync(MediaInfo info, MediaInfo? collectionInfo)
    {
        var interaction = context.Interaction;
        var embed = EmbedUtils.CreateMediaEmbed(info, collectionInfo);
        await interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed;
        });
    }

    public void UpdateContextAsync(IInteractionContext context)
    {
        this.context = context;
    }
}
