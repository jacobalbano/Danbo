using Danbo.Apis;
using Danbo.Models;
using Danbo.Services;
using Danbo.TypeConverters;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
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
public class ManagementModule : InteractionModuleBase<SocketInteractionContext>
{
    [RequireUserPermission(GuildPermission.ManageRoles)]
    [SlashCommand("user-role", "Add or remove a role from the assignable list")]
    public async Task Manage(
        [Summary(description: "The role to add/remove")] IRole role
    )
    {
        await WrapDeferred(() =>
        {
            if (userRoles.GetUserRoles().Contains(role.Id))
            {
                userRoles.DisableUserRole(role.Id);
                audit.Audit("Added user-assignable role", userId: Context.User.Id, detailId: role.Id, detailIdType: DetailIdType.Role);
                return "Role has been added";
            }
            else
            {
                audit.Audit("Removed user-assignable role", userId: Context.User.Id, detailId: role.Id, detailIdType: DetailIdType.Role);
                userRoles.EnableUserRole(role.Id);
                return "Role has been removed";
            }
        });
    }


    [RequireUserPermission(GuildPermission.ManageRoles)]
    [SlashCommand("ontopic", "Set or remove the ontopic role")]
    public async Task Ontopic(IRole role = null)
    {
        await WrapDeferred(() =>
        {
            ontopic.SetOntopicRoleId(role?.Id);
            if (role == null)
            {
                const string message = "Disabled Ontopic role";
                audit.Audit(message, userId: Context.User.Id);
                return message;
            }
            else
            {
                audit.Audit("Set Ontopic role", userId: Context.User.Id, detailId: role.Id, detailIdType: DetailIdType.Role);
                return $"Ontopic role set to {MentionUtils.MentionRole(role.Id)}";
            }
        });
    }

    [RequireUserPermission(ChannelPermission.ManageMessages)]
    [SlashCommand("tag", "Edit or remove a tag")]
    public async Task Tag([Autocomplete(typeof(TagsAutocomplete))] string tagName)
    {
        var tagContent = tags.GetTag(tagName);
        var modal = new ModalBuilder()
            .WithTitle(tagContent == null ? $"Adding {tagName}" : $"Editing {tagName}")
            .WithCustomId(Guid.NewGuid().ToString())
            .AddTextInput("Tag content", "content", TextInputStyle.Paragraph, required: false, value: tagContent)
            .Build();

        await RespondWithModalAsync(modal);

        try
        {
            var result = await modalHandler.WaitForModalAsync(modal);
            var defer = result.DeferAsync();

            var content = result.Data.Components
                .FirstOrDefault(x => x.CustomId == "content")
                ?? throw new Exception("Failed to find content component in modal");

            tagContent = string.IsNullOrWhiteSpace(content.Value) ? null : content.Value;
            tags.SetTag(tagName, tagContent);
            var message = tagContent == null
                ? "Tag removed"
                : "Tag saved";

            audit.Audit(message, userId: Context.User.Id, detailMessage: tagContent);

            await defer;
            await result.FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
                .WithDescription(message)
                .Build());
        }
        catch (TaskCanceledException) { }
        catch { throw; }
    }

    public ManagementModule(OntopicApi ontopic, TagsApi tags, UserRoleApi userRoles, AuditApi audit, ModalResponseService modalHandler)
    {
        this.ontopic = ontopic;
        this.tags = tags;
        this.userRoles = userRoles;
        this.audit = audit;
        this.modalHandler = modalHandler;
    }

    private async Task WrapDeferred(Func<Task<string>> task)
    {
        var defer = DeferAsync(ephemeral: true);
        var message = await task();
        await defer;
        await FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
            .WithDescription(message)
            .Build());
    }

    private async Task WrapDeferred(Func<string> task)
    {
        await WrapDeferred(() => Task.FromResult(task()));
    }

    private readonly OntopicApi ontopic;
    private readonly TagsApi tags;
    private readonly UserRoleApi userRoles;
    private readonly AuditApi audit;
    private readonly ModalResponseService modalHandler;
}
