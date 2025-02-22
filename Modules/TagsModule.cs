﻿using Danbo.Apis;
using Danbo.Errors;
using Danbo.Modules.Autocompletion;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

[Group("tags", "Display preset messages")]
[RequireContext(ContextType.Guild)]
public class TagsModule : ModuleBase
{

    [SlashCommand("tag", "Display the contents of a tag", ignoreGroupNames: true)]
    public async Task GetTag(
        [Autocomplete(typeof(TagsAutocomplete))] string tagName
    )
    {
        if (!tags.TryTakeWithCooldown(Context.Channel.Id, tagName))
        {
            await RespondAsync(ephemeral:true, embed: EmbedUtility.Message("This tag is currently on cooldown"));
            return;
        }

        var defer = DeferAsync();
        var tagText = tags.GetTag(tagName);

        await defer;
        if (tagText == null)
        {
            await FollowupAsync(embed: EmbedUtility.Error(
                "Invalid tag"
            ));
        }

        await FollowupAsync(tagText);
    }

    [SlashCommand("list", "Show a list of available tags")]
    public async Task ListTags()
    {
        var defer = DeferAsync();

        var sb = new StringBuilder();
        foreach (var t in tags.GetTags())
            sb.AppendLine($"- {t}");

        await defer;
        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription(sb.ToString())
            .Build());
    }

    public TagsModule(TagsApi tags)
    {
        this.tags = tags;
    }

    private readonly TagsApi tags;
}
