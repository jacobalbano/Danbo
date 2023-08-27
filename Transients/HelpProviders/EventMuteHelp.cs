using Danbo.Utility.DependencyInjection;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.HelpProviders;

[DependencyIgnore]
public class EventMuteHelp : IHelpProvider
{
    public string FeatureName => "Event Host Mute";

    public IAsyncEnumerable<string> FeaturesAvailable(IInteractionContext context)
    {
        return new[] { "Server-mute users from the context menu while hosting an event" }
            .ToAsyncEnumerable();
    }
}
