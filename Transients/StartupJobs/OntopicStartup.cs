using Danbo.Apis;
using Danbo.Models.Jobs;
using Danbo.Services;
using Discord;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.StartupJobs;

public class OntopicStartup : IStartupJob
{
    public Task Run(IGuild guild)
    {
        using var s = db.BeginSession();
        foreach (var job in s.Select<OntopicExpirationJob>().ToEnumerable())
        {
            scheduler.RemoveJob(job.JobHandle);
            var handle = scheduler.AddJob(job.Expiration, async () => {
                await guild.DownloadUsersAsync();
                var user = await guild.GetUserAsync(job.UserId);
                if (user == null)
                {
                    logger.LogWarning("Cannot remove ontopic from nonexistent member {userId}", job.UserId);
                    ontopicApi.RemoveOntopicExpiration(job.UserId);
                    return;
                }
                
                await ontopicApi.RemoveOntopicFromUser(user);
            });
            s.Update(job with { JobHandle = handle });
        }

        return Task.CompletedTask;
    }

    public OntopicStartup(GuildDb db, OntopicApi ontopicApi, SchedulerService scheduler, ILogger<OntopicStartup> logger)
    {
        this.db = db;
        this.ontopicApi = ontopicApi;
        this.scheduler = scheduler;
        this.logger = logger;
    }

    private readonly GuildDb db;
    private readonly OntopicApi ontopicApi;
    private readonly SchedulerService scheduler;
    private readonly ILogger logger;
}
