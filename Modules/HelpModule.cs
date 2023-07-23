using Danbo.Services;
using Danbo.Transients.HelpProviders;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

[RequireContext(ContextType.Guild)]
public class HelpModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("help", "Show bot features")]
    public async Task GetHelp()
    {
        var defer = DeferAsync();

        var builder = new EmbedBuilder();
        foreach (var p in providers)
        {
            builder.AddField(p.FeatureName, p.FeatureDescription);
        }

        await defer;
        await FollowupAsync(embed: builder.Build());
    }

    public HelpModule(IEnumerable<IHelpProvider> providers)
    {
        this.providers = providers
            .OrderBy(x => x.FeatureName)
            .ToList();
    }

    private readonly IReadOnlyList<IHelpProvider> providers;
}
