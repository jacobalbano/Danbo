using Danbo.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.HelpProviders;

internal class CommandsHelp : IHelpProvider
{
    public string FeatureName { get; } = "Commands";

    public async IAsyncEnumerable<string> FeaturesAvailable(IInteractionContext context)
    {
        var idDictTask = Task.Run(async () => (await guild.GetApplicationCommandsAsync()).ToDictionary(x => x.Name));
        foreach (var item in commands.SlashCommands)
        {
            var result = await item.CheckPreconditionsAsync(context, services);
            if (result.IsSuccess)
            {
                var dict = await idDictTask;
                var dictKey = item.Module.IsSlashGroup && !item.IgnoreGroupNames
                    ? item.Module.SlashGroupName
                    : item.Name;

                if (!dict.TryGetValue(dictKey, out var command))
                    continue;

                var displayName = item.Module.IsSlashGroup && !item.IgnoreGroupNames
                    ? item.ToString()
                    : item.Name;
                yield return $"</{displayName}:{command.Id}> - {item.Description}";
            }
        }
    }

    public CommandsHelp(DiscordSocketClient discord, ScopedGuildId guildId, InteractionService commands, IServiceProvider services)
    {
        this.commands = commands;
        this.services = services;
        if (guildId.Id is ulong id)
            guild = discord.GetGuild(id);
    }

    private readonly SocketGuild guild;
    private readonly InteractionService commands;
    private readonly IServiceProvider services;
}
