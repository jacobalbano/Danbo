using Danbo.Apis;
using Danbo.Models;
using Danbo.Models.Config;
using Danbo.Modules.Autocompletion;
using Danbo.Modules.Preconditions;
using Danbo.Services;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

[Group("manage", "Management commands for configuring the bot (mods only)")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ModerateMembers)]
public class ManagementModule : ModuleBase
{
    [SlashCommand("user-role", "Add or remove a role from the assignable list")]
    [RequireUserPermission(GuildPermission.ManageRoles), DefaultMemberPermissions(GuildPermission.ManageRoles)]
    public async Task Manage(
        [Summary(description: "The role to add/remove")] IRole role
    )
    {
        await WrapDeferred(() =>
        {
            if (userRoles.GetUserRoles().Contains(role.Id))
            {
                userRoles.DisableUserRole(role.Id);
                audit.Log("Removed user-assignable role", userId: Context.User.Id, detailId: role.Id, detailIdType: DetailIdType.Role);
                return "Role has been removed";
            }
            else
            {
                audit.Log("Added user-assignable role", userId: Context.User.Id, detailId: role.Id, detailIdType: DetailIdType.Role);
                userRoles.EnableUserRole(role.Id);
                return "Role has been added";
            }
        });
    }

    [SlashCommand("ontopic", "Set or remove the ontopic role")]
    [RequireUserPermission(GuildPermission.ManageRoles), DefaultMemberPermissions(GuildPermission.ManageRoles)]
    public async Task Ontopic(IRole role = null)
    {
        await WrapDeferred(() =>
        {
            ontopic.SetOntopicRoleId(role?.Id);
            if (role == null)
            {
                var message = "Disabled Ontopic role";
                audit.Log(message, userId: Context.User.Id);
                return message;
            }
            else
            {
                audit.Log("Set Ontopic role", userId: Context.User.Id, detailId: role.Id, detailIdType: DetailIdType.Role);
                return $"Ontopic role set to {MentionUtils.MentionRole(role.Id)}";
            }
        });
    }

    [SlashCommand("staff-channels", "Manage staff channels")]
    [RequireUserPermission(GuildPermission.ManageChannels), DefaultMemberPermissions(GuildPermission.ManageChannels)]
    public async Task StaffChannels(
        [Summary(description: "The setting to set or remove"), Autocomplete(typeof(ConfigClassAutocomplete<StaffChannelConfig>))] string property,
        IChannel channel = null
    )
    {
        await WrapDeferred(() =>
        {
            staffApi.SetStaffChannelProperty(property, channel?.Id);

            if (channel == null)
            {
                var message = $"Removed StaffChannel property {property}";
                audit.Log(message, userId: Context.User.Id);
                return message;
            }
            else
            {
                audit.Log("Set staff channel ID", userId: Context.User.Id, detailId: channel.Id, detailIdType: DetailIdType.Channel, detailMessage: property);
                return $"StaffChannel property {property} set to {MentionUtils.MentionChannel(channel.Id)}";
            }
        });
    }

    [SlashCommand("tag", "Edit or remove a tag")]
    [RequireUserPermission(GuildPermission.ManageMessages), DefaultMemberPermissions(GuildPermission.ManageMessages)]
    public async Task Tag([Autocomplete(typeof(TagsAutocomplete))] string tagName)
    {
        using var awaiter = new InteractionAwaiter(Context);
        var modalSignal = awaiter.Signal();

        var tagContent = tags.GetTag(tagName);
        await RespondWithModalAsync(new ModalBuilder()
            .WithTitle(tagContent == null ? $"Adding {tagName}" : $"Editing {tagName}")
            .WithCustomId(modalSignal.InteractionId)
            .AddTextInput("Tag content", "content", TextInputStyle.Paragraph, required: false, value: tagContent)
            .Build());

        await awaiter.HandleInteractionsAsync(async signal =>
        {
            if (signal != modalSignal)
                return;

            if (signal.Interaction is not SocketModal result)
                return;

            var defer = result.DeferAsync();
            var content = result.Data.Components
                .FirstOrDefault(x => x.CustomId == "content")
                ?? throw new Exception("Failed to find content component in modal");

            tagContent = string.IsNullOrWhiteSpace(content.Value) ? null : content.Value;
            tags.SetTag(tagName, tagContent);
            var message = tagContent == null
                ? "Tag removed"
                : "Tag saved";

            audit.Log(message, userId: Context.User.Id, detailMessage: tagContent);

            await defer;
            await result.FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
                .WithDescription(message)
                .Build());

            awaiter.Stop();
        });
    }

    public ManagementModule(OntopicApi ontopic, TagsApi tags, UserRoleApi userRoles, AuditApi audit, StaffApi staffApi)
    {
        this.ontopic = ontopic;
        this.tags = tags;
        this.userRoles = userRoles;
        this.audit = audit;
        this.staffApi = staffApi;
    }

    private readonly OntopicApi ontopic;
    private readonly TagsApi tags;
    private readonly UserRoleApi userRoles;
    private readonly AuditApi audit;
    private readonly StaffApi staffApi;
}
