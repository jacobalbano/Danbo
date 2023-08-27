using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.StartupJobs;

[AutoDiscoverImplementations]
interface IStartupJob
{
    Task Run(IGuild guild);
}
