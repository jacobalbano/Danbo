using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.HelpProviders;

[AutoDiscoverImplementations]
public interface IHelpProvider
{
    public string FeatureName { get; }

    public IAsyncEnumerable<string>  FeaturesAvailable(IInteractionContext context);
}
