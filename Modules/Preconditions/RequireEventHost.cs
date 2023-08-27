using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules.Preconditions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequireEventHostAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        if (context.User is IGuildUser user)
        {
            if (user.VoiceChannel == null)
                return PreconditionResult.FromError("You must be in a voice channel to use this command");

            var allEvents = await context.Guild.GetEventsAsync();
            var currentEvent = allEvents
                .Where(x => x.Status == GuildScheduledEventStatus.Active)
                .Where(x => x.Creator.Id == user.Id)
                .Where(x => user.VoiceChannel.Id == x.ChannelId)
                .FirstOrDefault();

            if (currentEvent == null)
                return PreconditionResult.FromError("You must be the creator of an active event to use this command");

            if (context.Interaction.Data is IUserCommandInteractionData data)
            {
                if (data.User == null ||
                    data.User is not IGuildUser member ||
                    member.VoiceChannel == null ||
                    member.VoiceChannel.Id != user.VoiceChannel.Id)
                    return PreconditionResult.FromError("This command can only be used on users in the same event channel");
            }

            return PreconditionResult.FromSuccess();
        }
        else
            return PreconditionResult.FromError("You must be in a guild to run this command.");
    }
}