﻿using Danbo.Apis;
using Danbo.Errors;
using Danbo.Models;
using Danbo.Modules.Autocompletion;
using Danbo.Modules.Parameters;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;
using System.Net.Http;
using System.Net.Mail;
using System.Reactive;
using System.Text;

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
        audit.Log("Mod note added", Context.User.Id, user.Id, DetailIdType.User, message);

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
        var defer = DeferAsync(ephemeral: true);

        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Warn, user.Id, message);
        audit.Log("User warned", Context.User.Id, user.Id, DetailIdType.User, message);

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
            await FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
                .WithAuthor(MakeAuthor(user))
                .WithTitle("User warned")
                .WithDescription(message)
                .Build());
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
        var defer = DeferAsync(ephemeral: true);
        var duration = unit.ToDuration(amount);
        var until = SystemClock.Instance.GetCurrentInstant() + duration;

        try { await user.SetTimeOutAsync(duration.ToTimeSpan()); }
        catch (Exception e)
        {
            logger.LogError(e, "Error timing user out");
            await FollowupAsync(ephemeral: true, embed: EmbedUtility.Error(
                "Error timing user out"
            ));
            return;
        }

        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Timeout, user.Id, message);
        audit.Log("User timed out", Context.User.Id, user.Id, DetailIdType.User, message);

        var embed = new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle($"You have been timed out until  <t:{until.ToUnixTimeSeconds()}:f>")
            .WithColor(infraction.Type.ToColor())
            .WithDescription(message);

        await Context.Channel.SendMessageAsync(MentionUtils.MentionUser(user.Id), embed: embed.Build());

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError(e, "Couldn't post to staff log"); }

        await defer;
        await FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("User timed out")
            .WithDescription(message)
            .Build());
    }

    [SlashCommand("ban", "Ban a user")]
    [RequireUserPermission(GuildPermission.BanMembers), DefaultMemberPermissions(GuildPermission.BanMembers)]
    public async Task Ban(
        [Summary(description: "The user to ban")] IUser user,
        [Summary(description: "A reason for the ban")] string message,
        [Summary(description: "Delete recent messages?")] bool? deleteMessages = null
    )
    {
        var defer = DeferAsync();

        await Context.Guild.AddBanAsync(user, pruneDays: deleteMessages == true ? 1 : 0, message);
        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Ban, user.Id, message);

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError("Couldn't post to staff log", e); }

        await defer;

        var embed = new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("User was banned")
            .WithDescription(message ?? "No reason given")
            .WithColor(infraction.Type.ToColor())
            .Build();

        var followup = await FollowupAsync(embed: embed);

        audit.Log("User banned", Context.User.Id, user.Id, DetailIdType.User, message);
        await FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("User banned")
            .Build());

        if (deleteMessages == null)
        {
            using var awaiter = new InteractionAwaiter(Context);
            var confirm = awaiter.Signal();
            var cancel = awaiter.Signal();

            var components = new ComponentBuilder()
                .WithButton("Purge recent messages", confirm.InteractionId, ButtonStyle.Danger)
                .WithButton("Cancel", cancel.InteractionId, ButtonStyle.Secondary);

            await ModifyOriginalResponseAsync(x => x.Components = components.Build());

            await awaiter.HandleInteractionsAsync(async signal =>
            {
                if (signal.Interaction is not SocketMessageComponent component)
                    return;

                if (signal.Interaction.User != Context.User)
                {
                    await signal.Interaction.RespondAsync("Only the user who ran the command can interact with it.", ephemeral: true);
                    return;
                }

                if (signal == cancel) awaiter.Stop();
                if (signal != confirm) return;

                await component.UpdateAsync(x => x.Components = new ComponentBuilder()
                    .WithButton("Purge in progress", confirm.InteractionId, ButtonStyle.Danger, disabled: true)
                    .WithButton("Cancel", cancel.InteractionId, ButtonStyle.Secondary, disabled: true)
                    .Build());

                var results = new List<IMessage>();
                var textChannels = Context.Guild.Channels
                    .Select(x => x as ITextChannel)
                    .Where(x => x != null)
                    .ToList();

                var threshold = SystemClock.Instance.GetCurrentInstant() - Duration.FromMinutes(30);
                int messageCount = 0, channelCount = 0;
                foreach (var chan in textChannels)
                {
                    try
                    {
                        var messages = (await chan.GetMessagesAsync()
                            .FlattenAsync())
                            .Where(x => x.Author == user)
                            .Where(x => x.Timestamp.ToInstant() >= threshold)
                            .ToList();
                        if (!messages.Any()) continue;

                        await chan.DeleteMessagesAsync(messages);
                        messageCount += messages.Count;
                        channelCount++;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Failed deleting messages from {channelId}", chan.Id);
                    }
                }

                await defer;
                audit.Log("Bulk delete after ban", Context.User.Id, detailId: user.Id, DetailIdType.User, (messageCount, channelCount));
                awaiter.Stop();
            });

            await followup.ModifyAsync(x => x.Components = new ComponentBuilder().Build());
        }
    }

    [SlashCommand("unban", "Unban a user")]
    [RequireUserPermission(GuildPermission.BanMembers), DefaultMemberPermissions(GuildPermission.BanMembers)]
    public async Task Unban(
        [Summary(description: "The user to unban")] IUser user,
        [Summary(description: "A reason for the unban")] string message
    )
    {
        var defer = DeferAsync();

        await Context.Guild.RemoveBanAsync(user);
        var infraction = rapsheet.Add(Context.User.Id, InfractionType.Note, user.Id, message);

        try { await staffApi.PostToStaffLog(infraction); }
        catch (Exception e) { logger.LogError("Couldn't post to staff log", e); }

        await defer;
        await FollowupAsync(embed: new EmbedBuilder()
            .WithAuthor(MakeAuthor(user))
            .WithTitle("User was unbanned")
            .WithDescription(message ?? "No reason given")
            .WithColor(infraction.Type.ToColor())
            .Build());
        audit.Log("User unbanned", Context.User.Id, user.Id, DetailIdType.User, message);
    }

    [SlashCommand("mass-kick", "Kick a large number of users at once with a modal or a file upload")]
    [RequireUserPermission(GuildPermission.BanMembers), DefaultMemberPermissions(GuildPermission.BanMembers)]
    public async Task MassKick(
        [Summary(description: "Kick reason")] string kickReason = null,
        [Summary(description: "A list of user IDs (one per line)")] IAttachment attachment = null
    )
    {
        kickReason ??= "No reason given";

        var idsString = string.Empty;
        if (attachment != null)
        {
            await DeferAsync();
            idsString = await http.GetStringAsync(attachment.Url);
        }
        else
        {
            using var awaiter = new InteractionAwaiter(Context);
            var modalSignal = awaiter.Signal();
            await RespondWithModalAsync(new ModalBuilder()
                .WithTitle("Mass kick")
                .WithCustomId(modalSignal.InteractionId)
                .AddTextInput("Kick reason", "reason", TextInputStyle.Short, required: true)
                .AddTextInput("User IDs (one per line)", "content", TextInputStyle.Paragraph, required: true)
                .Build());

            await awaiter.HandleInteractionsAsync(async signal =>
            {
                if (signal != modalSignal)
                    return;

                if (signal.Interaction is not SocketModal result)
                    return;

                await result.DeferAsync();
                idsString = result.Data.Components
                    .FirstOrDefault(x => x.CustomId == "content")
                    ?.Value.NullIfEmpty()
                    ?? throw new Exception("Failed to find content component in modal");

                kickReason = result.Data.Components
                    .FirstOrDefault(x => x.CustomId == "reason")
                    ?.Value.NullIfEmpty()
                    ?? throw new Exception("Failed to find reason component in modal");

                awaiter.Stop();
            });
        }

        var idsToKick = idsString.Split("\r\n")
            .Select(x => (success: ulong.TryParse(x, out var value), value))
            .Where(x => x.success)
            .Select(x => x.value)
            .ToList();

        if (!idsToKick.Any())
        {
            await FollowupAsync("No IDs were supplied");
            return;
        }

        var message = await FollowupAsync("Beginning mass kick, please wait");

        var baseDelay = TimeSpan.FromSeconds(1) / 50;
        var delay = baseDelay;
        var ratelimit = new RequestOptions {
            RatelimitCallback = async info => delay = TimeSpan.FromSeconds((info.RetryAfter ?? 1) + 1)
        };

        var kicked = new HashSet<ulong>();
        await Context.Guild.DownloadUsersAsync();

        foreach (var id in idsToKick)
        {
            var user = Context.Guild.GetUser(id);
            if (user == null) continue;

            try
            {
                await user.KickAsync(options: ratelimit);
                rapsheet.Add(Context.User.Id, InfractionType.Note, id, $"Mass-kicked with reason: {kickReason}");
                kicked.Add(id);
            }
            catch
            {
            }

            await Task.Delay(delay);
            delay = baseDelay;
        }

        await message.ReplyAsync($"Mass-kicked {kicked.Count} users: {kickReason}");
    }

    [SlashCommand("rapsheet", "Display's a user's record")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task Rapsheet(
        [Summary(description: "The user to view")] IUser user
    )
    {
        var defer = DeferAsync();
        audit.Log("Rapsheet viewed", Context.User.Id, user.Id, DetailIdType.User);
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
        audit.Log("Rapsheet searched", Context.User.Id, detailObject: (infractionType, keyword));
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
            await FollowupAsync(embed: EmbedUtility.Error(
                "Invalid infraction key"
            ));
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
        var followup = await FollowupAsync(embed: embed.Build(), components: components.Build());

        await awaiter.HandleInteractionsAsync(async signal =>
        {
            if (signal.Interaction is not SocketMessageComponent component)
                return;

            if (signal == cancel)
            {
                await component.UpdateAsync(x => (x.Embed, x.Components) = Clear("Cancelled"));
                awaiter.Stop();
            }

            if (signal != confirm) return;
            var oldRecord = rapsheet.Remove(user.Id, key);
            if (oldRecord != null)
            {
                audit.Log("Removed infraction", Context.User.Id, detailId: user.Id, detailIdType: DetailIdType.User, detailMessage: oldRecord.Message);
                await component.UpdateAsync(x => (x.Embed, x.Components) = Clear("Infraction removed"));
            }
            else
            {
                await component.UpdateAsync(x => (x.Embed, x.Components) = Clear("Error removing infraction"));
            }

            awaiter.Stop();
        });

        await followup.ModifyAsync(x => x.Components = new ComponentBuilder().Build());

        static (Embed, MessageComponent) Clear(string message) => (
            new EmbedBuilder().WithDescription(message).Build(),
            new ComponentBuilder().Build()
        );
    }

    //[MessageCommand("Move Message")]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task MoveMessage(IMessage message)
    {
        using var awaiter = new InteractionAwaiter(Context);
        var modalSignal = awaiter.Signal();

        await RespondAsync("Select a channel", ephemeral: true, components: new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithType(ComponentType.ChannelSelect)
                .WithPlaceholder("Select a channel")
                .WithCustomId(modalSignal.InteractionId)
                .WithChannelTypes(ChannelType.Text)
                .WithMinValues(1)
                .WithMaxValues(1)
            ).Build());

        await awaiter.HandleInteractionsAsync(async signal =>
        {
            if (signal != modalSignal)
                return;

            if (signal.Interaction is not SocketMessageComponent result)
                return;

            var moveTo = result.Data.Channels.FirstOrDefault() as ITextChannel;
            var defer = result.DeferAsync();

            try
            {
                var avatar = await http.GetStreamAsync(message.Author.GetAvatarUrl());
                var hook = await moveTo.CreateWebhookAsync(message.Author.Username, avatar);

                ulong newMessageId = 0;
                using var hookClient = new DiscordWebhookClient(hook);
                if (message.Attachments.Any())
                {
                    using var resend = new ResendAttachments(http, message);
                    newMessageId = await hookClient.SendFilesAsync(await resend.GetAttachments(),
                        text: message.Content,
                        username: message.Author.Username
                    );
                }
                else
                {
                    newMessageId = await hookClient.SendMessageAsync(
                        text: message.Content,
                        username: message.Author.Username
                    );
                }

                await hookClient.DeleteWebhookAsync();

                var newMessage = await moveTo.GetMessageAsync(newMessageId);
                await result.FollowupAsync("Message moved", ephemeral: true);
                await moveTo.SendMessageAsync($"{message.Author.Mention}, your message was moved here from {MentionUtils.MentionChannel(message.Channel.Id)}", messageReference: new(newMessage.Id));
                await message.DeleteAsync();

                awaiter.Stop();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to move message");
                await defer;
                await result.FollowupAsync("There was an error moving the message");
            }
        });
    }

    private static Embed[] MakeEmbeds(IMessage message)
    {
        var list = message.Embeds.Select(x => x.ToEmbedBuilder().Build()).ToList();
        if (!string.IsNullOrEmpty(message.Content))
        {
            list.Insert(0, new EmbedBuilder()
                .WithAuthor(message.Author)
                .WithDescription(message.Content)
                .Build());
        }
        
        return list.ToArray();
    }

    private class ResendAttachments : IDisposable
    {
        public ResendAttachments(HttpClient http, IMessage message)
        {
            this.http = http;
            this.message = message;
        }

        public void Dispose()
        {
            foreach (var stream in streams)
                stream.Dispose();
            streams.Clear();
        }

        public async Task<FileAttachment[]> GetAttachments()
        {
            int i = 0;
            var result = new FileAttachment[message.Attachments.Count];
            foreach (var attachment in message.Attachments)
            {
                var ms = new MemoryStream();
                using var response = await http.GetAsync(attachment.Url);
                var stream = await response.Content.ReadAsStreamAsync();
                stream.CopyTo(ms);
                ms.Position = 0;

                streams.Add(ms);
                result[i++] = new FileAttachment(ms, attachment.Filename, attachment.Description, attachment.IsSpoiler());
            }

            return result;
        }

        private List<MemoryStream> streams = new();
        private readonly HttpClient http;
        private readonly IMessage message;
    }

    public ModeratorModule(RapsheetApi rapsheet, AuditApi audit, StaffApi staffApi, ILogger<ModeratorModule> logger, HttpClient http)
    {
        this.rapsheet = rapsheet;
        this.audit = audit;
        this.staffApi = staffApi;
        this.logger = logger;
        this.http = http;
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
        var followup = await FollowupAsync(embed: embed, components: components);

        await handler.HandleInteractionsAsync(async signal =>
        {
            if (signal.Interaction is not SocketMessageComponent component)
                return;

            if (signal == firstSignal) page = 0;
            else if (signal == prevSignal) page--;
            else if (signal == nextSignal) page++;
            else if (signal == lastSignal) page = (messages.Count() / 5) - 1;
            else return;

            await component.UpdateAsync(x => (x.Embed, x.Components) = build());
        });

        await followup.ModifyAsync(x => x.Components = new ComponentBuilder().Build());

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
    private readonly ILogger logger;
    private HttpClient http;

    private static EmbedAuthorBuilder MakeAuthor(IUser user) => new EmbedAuthorBuilder()
        .WithName(user.Username)
        .WithIconUrl(user.GetAvatarUrl());

    private static EmbedFooterBuilder MakeRapsheetFooter() => new EmbedFooterBuilder()
        .WithText("📝: note, ⚠️: warn, 🛑: timeout, 🔨: ban");
}
