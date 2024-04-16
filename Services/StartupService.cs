using Danbo.Apis;
using Danbo.Errors;
using Danbo.Models.Config;
using Danbo.Models.Jobs;
using Danbo.Transients.StartupJobs;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
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
public class StartupService
{
    public StartupService(DiscordSocketClient discord, SchedulerService scheduler, IServiceProvider services)
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

    private async Task Ready()
    {
        foreach (var guild in discord.Guilds)
        {
            using var scope = services.GuildScope(guild.Id);

            foreach (var init in scope.ServiceProvider.GetServices<IStartupJob>())
                await init.Run(guild);
        }

        discord.Ready -= Ready;
    }

    private readonly DiscordSocketClient discord;
    private readonly SchedulerService scheduler;
    private readonly IServiceProvider services;
}
