using Danbo.Apis;
using Danbo.Errors;
using Danbo.Models;
using Danbo.Modules.Autocompletion;
using Danbo.Services;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

[Group("role", "Manage your user-assignable roles")]
[RequireContext(ContextType.Guild)]
public partial class UserRoleModule : ModuleBase
{
    [SlashCommand("remove", "Remove a user-assigned role from yourself")]
    public async Task Remove(
        [Summary(description: "The role you want to remove from yourself"), Autocomplete(typeof(UserRoleAutocomplete))] string role
    )
    {
        var defer = DeferAsync();
        var validRoles = userRoles
            .GetUserRoles()
            .ToHashSet();

        await defer;
        if (!ulong.TryParse(role, out var id) || !validRoles.Contains(id))
            throw new FollowupError("Invalid role selected");

        if (Context.User is IGuildUser user)
        {
            if (!user.RoleIds.Contains(id))
                throw new FollowupError("You don't currently have that role");
            await user.RemoveRoleAsync(id);
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription($"Removed {MentionUtils.MentionRole(id)}")
            .Build()
        );
    }

    [SlashCommand("assign", "Assign a user-assigned role to yourself")]
    public async Task Assign(
        [Summary(description: "The role you want to assign to yourself"), Autocomplete(typeof(UserRoleAutocomplete))] string role
    )
    {
        var defer = DeferAsync();
        var validRoles = userRoles
            .GetUserRoles()
            .ToHashSet();

        await defer;
        if (!ulong.TryParse(role, out var id) || !validRoles.Contains(id))
        {
            audit.Log("Invalid role assignment", userId: Context.User.Id, detailMessage: role, detailId: id, detailIdType: DetailIdType.Role );
            throw new FollowupError("Invalid role selected");
        }

        if (Context.User is IGuildUser user)
        {
            if (user.RoleIds.Contains(id))
                throw new FollowupError("You already have that role");
            await user.AddRoleAsync(id);
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription($"Added {MentionUtils.MentionRole(id)}")
            .Build()
        );
    }

    [SlashCommand("list", "Show user-assignable roles")]
    public async Task List()
    {
        var defer = DeferAsync();

        var sb = new StringBuilder();
        var allRoles = userRoles.GetUserRoles();
        if (allRoles.Any())
        {
            sb.AppendLine("The following roles are available for self-assignment:");
            foreach (var role in allRoles)
                sb.AppendLine($"- {MentionUtils.MentionRole(role)}");
        }
        else
        {
            sb.Append("No user-assignable roles have been added");
        }

        await defer;
        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription(sb.ToString())
            .Build());
    }

    public UserRoleModule(AuditApi audit, UserRoleApi userRoles)
    {
        this.audit = audit;
        this.userRoles = userRoles;
    }

    private readonly AuditApi audit;
    private readonly UserRoleApi userRoles;
}
