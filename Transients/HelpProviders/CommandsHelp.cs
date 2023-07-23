using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.HelpProviders;

internal class CommandsHelp : IHelpProvider
{
    public string FeatureName { get; } = "Commands";

    public string FeatureDescription { get; } = "Type / to explore the commands available";
}
