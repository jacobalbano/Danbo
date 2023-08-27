using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.HelpProviders;

internal class UserPinsHelp : IHelpProvider
{
    public string FeatureName { get; } = "User Pins";

    public IAsyncEnumerable<string> FeaturesAvailable(IInteractionContext context)
    {
        return new[] { "React with the 📌 emoji to pin messages in a thread you created" }
            .ToAsyncEnumerable();
    }
}
