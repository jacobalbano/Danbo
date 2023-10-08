using Danbo.Apis;
using Danbo.Errors;
using Danbo.Models;
using Danbo.Modules.Autocompletion;
using Danbo.Modules.Parameters;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using NodaTime;
using System.Text;
using System.Text.Json;

namespace Danbo.Modules;

[Group("mod", "Management commands for configuring the bot (mods only)")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ModerateMembers)]
public class ModeratorModule : ModuleBase
{
    [SlashCommand("note", "Add a note to a user's record without alerting them")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task Note(
        [Summary(description: "The user to add a note to")] IUser user,
        [Summary(description: "The note to add")] string message
    )
    {
        var defer = DeferAsync(ephemeral: true);

        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Note, user.Id, message);
        audit.Audit("Mod note added", Context.User.Id, user.Id, DetailIdType.User, message);

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError("Couldn't post to staff log", e); }

        await defer;
        await FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("Note added")
            .WithDescription(message)
        .Build());
    }

    [SlashCommand("warn", "Publicly warn a user for an infringement")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task Warn(
        [Summary(description: "The user to warn")] IUser user,
        [Summary(description: "The warn reason")] string message
    )
    {
        var defer = DeferAsync();

        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Warn, user.Id, message);
        audit.Audit("User warned", Context.User.Id, user.Id, DetailIdType.User, message);

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError("Couldn't post to staff log", e); }

        var embed = new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("You have been warned")
            .WithDescription(message)
            .WithColor(infraction.Type.ToColor());

        try
        {
            await defer;
            await Context.Channel.SendMessageAsync(MentionUtils.MentionUser(user.Id), embed: embed.Build());
            await FollowupAsync("_ _");
        }
        catch (HttpException)
        {
            await FollowupAsync(embed: embed.Build());
        }
    }

    [SlashCommand("timeout", "Time a user out")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task Timeout(
        [Summary(description: "The user to timeout")] IGuildUser user,
        [Summary(description: "A reason for the timeout")] string message,
        int amount,
        DurationUnit unit
    )
    {
        var defer = DeferAsync();
        var duration = unit.ToDuration(amount);
        var until = SystemClock.Instance.GetCurrentInstant() + duration;

        try { await user.SetTimeOutAsync(duration.ToTimeSpan()); }
        catch (Exception) { throw new FollowupError("Error timing user out"); }

        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Timeout, user.Id, message);
        audit.Audit("User timed out", Context.User.Id, user.Id, DetailIdType.User, message);

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError("Couldn't post to staff log", e); }

        var embed = new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle($"You have been timed out until  <t:{until.ToUnixTimeSeconds()}:f>")
            .WithColor(infraction.Type.ToColor())
            .WithDescription(message);

        await defer;
        await FollowupAsync(embed: embed.Build(), allowedMentions: new AllowedMentions(AllowedMentionTypes.Users));
    }

    [SlashCommand("ban", "Ban a user")]
    [RequireUserPermission(GuildPermission.BanMembers), DefaultMemberPermissions(GuildPermission.BanMembers)]
    public async Task Ban(
        [Summary(description: "The user to ban")] IUser user,
        [Summary(description: "A reason for the ban")] string message,
        [Summary(description: "Delete recent messages?")] bool deleteMessages = false
    )
    {
        var defer = DeferAsync();

        await Context.Guild.AddBanAsync(user, pruneDays: deleteMessages ? 1 : 0, message);
        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Ban, user.Id, message);

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError("Couldn't post to staff log", e); }

        await defer;
        await FollowupAsync(embed: new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("User was banned")
            .WithDescription(message ?? "No reason given")
            .WithColor(infraction.Type.ToColor())
            .Build());
        audit.Audit("User banned", Context.User.Id, user.Id, DetailIdType.User, message);
    }

    [SlashCommand("rapsheet", "Display's a user's record")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task Rapsheet(
        [Summary(description: "The user to view")] IUser user
    )
    {
        var defer = DeferAsync();
        audit.Audit("Rapsheet viewed", Context.User.Id, user.Id, DetailIdType.User);
        var messages = rapsheet.GetInfractionsForUser(user)
            .OrderByDescending(x => x.InfractionInstant)
            .Select(x => $"{x.Type.ToEmoji()} by {MentionUtils.MentionUser(x.ModeratorId)} (<t:{x.InfractionInstant.ToUnixTimeSeconds()}:d>) - {x.Message}");

        await defer;
        await ShowRapsheetTable(messages);
    }

    [SlashCommand("rapsheet-search", "Search for rapsheet entries by type or keyword")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task RapsheetSearch(
        [Summary(description: "Type of infraction to search by")] InfractionType? infractionType = null,
        [Summary(description: "A word or phrase to search by")] string keyword = null
    )
    {
        var defer = DeferAsync();
        audit.Audit("Rapsheet searched", Context.User.Id, detailObject: (infractionType, keyword));
        var messages = rapsheet.SearchInfractions(infractionType, keyword)
            .OrderByDescending(x => x.InfractionInstant)
            .Select(x => $"{x.Type.ToEmoji()} by {MentionUtils.MentionUser(x.ModeratorId)} (<t:{x.InfractionInstant.ToUnixTimeSeconds()}:d> ) {MentionUtils.MentionUser(x.UserId)} -  {x.Message}");

        await defer;
        await ShowRapsheetTable(messages);
    }

    [SlashCommand("remove-infraction", "Remove an entry from a user's record")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task RemoveInfraction(
        [Summary(description: "The user to remove an infraction from")] IUser user,
        [Summary(description: "The infraction key (search with auto-complete)"), Autocomplete(typeof(InfractionAutocomplete))] string infractionKey
    )
    {
        var defer = DeferAsync();
        if (!Guid.TryParse(infractionKey, out var key))
        {
            await defer;
            throw new FollowupError("Invalid infraction key");
        }

        using var awaiter = new InteractionAwaiter(Context);
        var confirm = awaiter.Signal();
        var cancel = awaiter.Signal();

        var embed = new EmbedBuilder()
            .WithTitle("Really delete this record?")
            .WithColor(Color.Orange);

        var components = new ComponentBuilder()
            .WithButton("Delete", confirm.InteractionId, ButtonStyle.Danger)
            .WithButton("Cancel", cancel.InteractionId, ButtonStyle.Secondary);

        await defer;
        await FollowupAsync(embed: embed.Build(), components: components.Build());

        while (awaiter.IsValid)
        {
            var signal = await awaiter.WaitForSignal();
            if (signal.Interaction is not SocketMessageComponent component)
                continue;

            await component.DeferAsync();
            if (signal == cancel)
            {
                await component.UpdateAsync(x => (x.Embed, x.Components) = Clear("Cancelled"));
                return;
            }

            if (signal != confirm) continue;
            var oldRecord = rapsheet.Remove(user.Id, key);
            if (oldRecord != null)
            {
                audit.Audit("Removed infraction", Context.User.Id, detailId: user.Id, detailIdType: DetailIdType.User, detailMessage: oldRecord.Message);
                await component.UpdateAsync(x => (x.Embed, x.Components) = Clear("Infraction removed"));
            }
            else
            {
                await component.UpdateAsync(x => (x.Embed, x.Components) = Clear("Error removing infraction"));
            }

            break;
        }

        static (Embed, MessageComponent) Clear(string message) => (
            new EmbedBuilder().WithDescription(message).Build(),
            new ComponentBuilder().Build()
        );
    }

    public ModeratorModule(RapsheetApi rapsheet, AuditApi audit, StaffApi staffApi, ILogger<ModeratorModule> logger)
    {
        this.rapsheet = rapsheet;
        this.audit = audit;
        this.staffApi = staffApi;
        this.logger = logger;
    }

    private async Task ShowRapsheetTable(IEnumerable<string> messages)
    {
        int page = 0;
        using var handler = new InteractionAwaiter(Context);
        var firstSignal = handler.Signal();
        var prevSignal = handler.Signal();
        var nextSignal = handler.Signal();
        var lastSignal = handler.Signal();
        var build = () => BuildRapsheet(messages, page, firstSignal, prevSignal, nextSignal, lastSignal);

        var (embed, components) = build();
        await FollowupAsync(embed: embed, components: components);

        while (handler.IsValid)
        {
            var button = await handler.WaitForSignal();
            if (button.Interaction is not SocketMessageComponent component)
                continue;

            if (button == firstSignal) page = 0;
            else if (button == prevSignal) page--;
            else if (button == nextSignal) page++;
            else if (button == lastSignal) page = (messages.Count() / 5) - 1;
            else continue;
            
            await component.UpdateAsync(x => (x.Embed, x.Components) = build());
        }

        static (Embed, MessageComponent) BuildRapsheet(IEnumerable<string> infractions, int page, IInteractionSignal firstSignal, IInteractionSignal prevSignal, IInteractionSignal nextSignal, IInteractionSignal lastSignal)
        {
            var list = new List<string>();
            var e = infractions
                .Skip(page * 5)
                .GetEnumerator();

            int limit = 5;
            bool canGoNext = e.MoveNext();
            while (canGoNext && limit-- > 0)
            {
                list.Add(e.Current);
                canGoNext = e.MoveNext();
            }

            if (!e.MoveNext())
                canGoNext = false;

            var embed = new EmbedBuilder()
                .WithTitle(list.Any()
                    ? $"Rapsheet search (Showing results {page * 5 + 1} - {page * 5 + list.Count} of {infractions.Count()})"
                    : "No results")
            .WithFooter(MakeRapsheetFooter())
                .WithDescription(list.Aggregate(new StringBuilder(), (sb, x) => sb.AppendLine(x))
                    .ToString());

            var components = new ComponentBuilder()
                .WithButton("<<", firstSignal.InteractionId, disabled: page == 0)
                .WithButton("Previous", prevSignal.InteractionId, disabled: page == 0)
                .WithButton("Next", nextSignal.InteractionId, disabled: !canGoNext)
                .WithButton(">>", lastSignal.InteractionId, disabled: !canGoNext);

            return (embed.Build(), components.Build());
        }
    }

    private readonly RapsheetApi rapsheet;
    private readonly AuditApi audit;
    private readonly StaffApi staffApi;
    private readonly ILogger<ModeratorModule> logger;

    private static EmbedAuthorBuilder MakeAuthor(IUser user) => new EmbedAuthorBuilder()
        .WithName(user.Username)
        .WithIconUrl(user.GetAvatarUrl());

    private static EmbedFooterBuilder MakeRapsheetFooter() => new EmbedFooterBuilder()
        .WithText("📝: note, ⚠️: warn, 🛑: timeout, 🔨: ban");
}
