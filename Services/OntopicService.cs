using Danbo.Apis;
using Danbo.Errors;
using Danbo.Models.Config;
using Danbo.Models.Jobs;
using Danbo.Utility;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Services;

[AutoDiscoverSingletonService, ForceInitialization]
public class OntopicService
{
    public OntopicService(DiscordSocketClient discord, SchedulerService scheduler, IServiceProvider services)
    {
        discord.Ready += Ready;
        discord.Connected += Discord_Connected;
        this.discord = discord;
        this.scheduler = scheduler;
        this.services = services;
    }

    private Task Discord_Connected()
    {
        // TODO: do we need to refresh anything?
        return Task.CompletedTask;
    }

    private Task Ready()
    {
        foreach (var guild in discord.Guilds)
        {
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ScopedGuildId>()
                .Initialize(guild.Id);

            var db = scope.ServiceProvider.GetRequiredService<Database>();
            var api = scope.ServiceProvider.GetRequiredService<OntopicApi>();
            var s = db.BeginSession();
            foreach (var job in s.Select<OntopicExpirationJob>().ToEnumerable())
            {
                scheduler.RemoveJob(job.JobHandle);
                var handle = scheduler.AddJob(job.Expiration, async () => await api.RemoveOntopicFromUser(await guild.GetUserAsync(job.UserId)));
                s.Update(job with { JobHandle = handle });
            }
        }

        discord.Ready -= Ready;
        return Task.CompletedTask;
    }

    private readonly DiscordSocketClient discord;
    private readonly SchedulerService scheduler;
    private readonly IServiceProvider services;
}
