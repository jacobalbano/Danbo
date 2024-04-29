using Danbo.Models.Jobs;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.StartupJobs;

public class AnalysisStartup : IStartupJob
{
    public AnalysisStartup(GuildDb db)
    {
        this.db = db;
    }

    public Task Run(IGuild guild)
    {
        using var s = db.BeginSession();

        s.Update(s.Select<AnalysisJob>()
            .Where(x => x.State == AnalysisState.Running)
            .ToEnumerable()
            .Select(x => x with { State = AnalysisState.Paused }));

        return Task.CompletedTask;
    }

    private readonly GuildDb db;
}
