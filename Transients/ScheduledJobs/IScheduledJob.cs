using Danbo.Utility.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.ScheduledJobs;

[AutoDiscoverImplementations]
public interface IScheduledJob
{
}

public interface IScheduledJob<TSelf, TConfig> : IScheduledJob
    where TSelf : IScheduledJob<TSelf, TConfig>
{

}
