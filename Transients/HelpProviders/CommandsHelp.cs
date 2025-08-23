using Danbo.Services;
using Danbo.Utility;
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
        foreach (var item in commands.SlashCommands)
        {
            var result = await item.CheckPreconditionsAsync(context, services);
            if (result.IsSuccess)
            {
                var embed= await commandUtil.GetCommandMention(item);
                yield return $"{embed} - {item.Description}";
            }
        }
    }

    public CommandsHelp(InteractionService commands, IServiceProvider services, CommandUtility commandUtil)
    {
        this.commands = commands;
        this.services = services;
        this.commandUtil = commandUtil;
    }

    private readonly InteractionService commands;
    private readonly IServiceProvider services;
    private readonly CommandUtility commandUtil;
}
