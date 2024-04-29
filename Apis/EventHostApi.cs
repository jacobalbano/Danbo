using Danbo.Services;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class EventHostApi
{
    public async Task<bool> ToggleMute(IGuildUser user, IGuildScheduledEvent guildEvent)
    {
        await user.ModifyAsync(x => x.Mute = !user.IsMuted);

        var s = db.BeginSession();
        if (user.IsMuted)
        {
        }

        return user.IsMuted;
    }

    public EventHostApi(GuildDb db, SchedulerService scheduler)
    {
        this.db = db;
        this.scheduler = scheduler;
    }

    private readonly GuildDb db;
    private readonly SchedulerService scheduler;
}
