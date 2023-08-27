using Danbo.Apis;
using Danbo.Errors;
using Danbo.Modules.Preconditions;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

[RequireContext(ContextType.Guild)]
public class EventHostModule : ModuleBase
{
    [RequireEventHost]
    //[UserCommand("Event Host Mute")]
    public async Task MuteUser(IGuildUser user)
    {
        var defer = DeferAsync();
        var currentEvent = (await Context.Guild.GetEventsAsync())
            .Where(x => x.Status == GuildScheduledEventStatus.Active)
            .Where(x => x.Creator.Id == user.Id)
            .Where(x => user.VoiceChannel.Id == x.ChannelId)
            .FirstOrDefault();

        try
        {

            var wasMuted = await eventHost.ToggleMute(user, currentEvent);

            await defer;
            await FollowupAsync(embed: new EmbedBuilder()
                .WithDescription($"{user.Mention} has been {(wasMuted ? "" : "un")}muted")
                .Build());
        }
        catch (Exception)
        {
            await defer;
            throw new FollowupError("Couldn't mute/unmute the user");
        }
    }

    public EventHostModule(EventHostApi eventHost)
    {
        this.eventHost = eventHost;
    }

    private readonly EventHostApi eventHost;
}
