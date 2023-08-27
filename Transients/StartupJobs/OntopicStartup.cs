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

public class OntopicStartup : IStartupJob
{
    public Task Run(IGuild guild)
    {
        var s = db.BeginSession();
        foreach (var job in s.Select<OntopicExpirationJob>().ToEnumerable())
        {
            scheduler.RemoveJob(job.JobHandle);
            var handle = scheduler.AddJob(job.Expiration, async () => await ontopicApi.RemoveOntopicFromUser(await guild.GetUserAsync(job.UserId)));
            s.Update(job with { JobHandle = handle });
        }

        return Task.CompletedTask;
    }

    public OntopicStartup(Database db, OntopicApi ontopicApi, SchedulerService scheduler)
    {
        this.db = db;
        this.ontopicApi = ontopicApi;
        this.scheduler = scheduler;
    }

    private readonly Database db;
    private readonly OntopicApi ontopicApi;
    private readonly SchedulerService scheduler;
}
