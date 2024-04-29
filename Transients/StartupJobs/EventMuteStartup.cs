using Danbo.Apis;
using Danbo.Models.Jobs;
using Danbo.Services;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.StartupJobs;

public class EventMuteStartup : IStartupJob
{
    public async Task Run(IGuild guild)
    {
        using var s = db.BeginSession();
        foreach (var job in s.Select<EventMuteExpirationJob>().ToEnumerable())
        {
            var user = await guild.GetUserAsync(job.UserId);
            if (user == null)
            {
                // probably left while muted. nothing we can do
                s.Delete(job);
                continue;
            }

            var evt = await guild.GetEventAsync(job.GuildEventId);
            if (evt == null || evt.Status != GuildScheduledEventStatus.Active)
                await user.ModifyAsync(x => x.Mute = false);
        }
    }

    public EventMuteStartup(GuildDb db)
    {
        this.db = db;
    }

    private readonly GuildDb db;
}
