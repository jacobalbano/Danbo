using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Danbo.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Danbo.Services;
using Danbo.Errors;
using System.Collections.Concurrent;
using Danbo.Apis;
using Danbo.Utility.DependencyInjection;

namespace Danbo.Modules;

[AutoDiscoverSingletonService]
public class CommandHandlerService
{
    private readonly InteractionService commands;
    private readonly DiscordSocketClient discord;
    private readonly ILogger logger;
    private readonly IServiceProvider services;

    public CommandHandlerService(InteractionService commands, DiscordSocketClient discord, ILogger<CommandHandlerService> logger, IServiceProvider services)
    {
        this.commands = commands;
        this.discord = discord;
        this.logger = logger;
        this.services = services;
    }

    public async Task Initialize()
    {
        try
        {
            await commands.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
            discord.InteractionCreated += InteractionCreated;
            discord.Ready += Ready;
            discord.JoinedGuild += JoinedGuild;
            commands.InteractionExecuted += Commands_InteractionExecuted;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error initializing command handler");
            throw;
        }
    }

    private async Task Ready()
    {
        await RegisterCommandsToAllGuilds();
        discord.Ready -= Ready;
    }

    private async Task InteractionCreated(SocketInteraction arg)
    {
        var ctx = new SocketInteractionContext(discord, arg);
        var scope = services.GuildScope(arg.GuildId.Value);
        needCleanup.TryAdd(arg.Id, scope);

        await commands.ExecuteCommandAsync(ctx, scope.ServiceProvider);
    }

    private async Task Commands_InteractionExecuted(ICommandInfo arg1, IInteractionContext arg2, IResult arg3)
    {
        if (needCleanup.TryRemove(arg2.Interaction.Id, out var disposable))
            disposable.Dispose();

        if (arg3.IsSuccess) return;

        if (arg3 is PreconditionResult result)
        {
            await arg2.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithColor(Color.Red)
                .WithDescription(result.ErrorReason)
                .Build());

            using var scope = services.GuildScope(arg2.Guild.Id);
            scope.ServiceProvider.GetRequiredService<AuditApi>()
                .Log("Attempted to run a privileged command", arg2.User.Id, detailMessage: arg1.Name);
        }
    }

    private async Task JoinedGuild(SocketGuild guild)
    {
        await RegisterCommandsToGuild(guild.Id);
    }

    private async Task RegisterCommandsToAllGuilds()
    {
        foreach (var guild in discord.Guilds)
            await RegisterCommandsToGuild(guild.Id);
    }

    private async Task RegisterCommandsToGuild(ulong guildId)
    {
        await commands.RegisterCommandsToGuildAsync(guildId, deleteMissing: true);
    }

    private readonly ConcurrentDictionary<ulong, IDisposable> needCleanup = new();
}