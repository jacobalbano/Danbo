using Danbo.Utility;
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

    public string FeatureDescription { get; }
}
