﻿using Danbo.Apis;
using Danbo.Errors;
using Danbo.Services;
using Discord;
using Discord.Interactions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Danbo.Modules;

[RequireContext(ContextType.Guild)]
public class OntopicModule : InteractionModuleBase<SocketInteractionContext>
{
    public enum Unit
    {
        Minutes,
        Hours,
        Days
    }

    [SlashCommand("ontopic", "Block access to offtopic channels for a given amount of time")]
    public async Task GoOntopic(int amount, Unit unit)
    {
        var defer = DeferAsync();
        var duration = unit switch
        {
            Unit.Minutes => Duration.FromMinutes(amount),
            Unit.Hours => Duration.FromHours(amount),
            Unit.Days => Duration.FromDays(amount),
            _ => throw new FollowupError("Invalid unit parameter")
        };

        await defer;
        if (duration > maxDuration)
            throw new FollowupError("Please limit your use of this command to intervals of 30 days or less");

        var expiration = SystemClock.Instance.GetCurrentInstant() + duration;
        await ontopic.AddOntopicToUser((IGuildUser)Context.User, expiration);

        var embed = new EmbedBuilder()
            .WithDescription($"Ontopic applied until <t:{expiration.ToUnixTimeSeconds()}:f>")
            .Build();

        await FollowupAsync(embed: embed);

        try { await Context.User.SendMessageAsync(embed: embed); }
        catch (Exception) { }
    }

    public OntopicModule(OntopicApi ontopic)
    {
        this.ontopic = ontopic;
    }

    private static readonly Duration maxDuration = Duration.FromDays(30);
    private readonly OntopicApi ontopic;
}
