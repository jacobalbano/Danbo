using Danbo.Utility.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.ScheduledJobs;

[DependencyIgnore]
public class RuntimeScheduledJob : IScheduledJob<RuntimeScheduledJob, RuntimeScheduledJob.Config>
{
    public class Config { };

    public RuntimeScheduledJob()
    {
        
    }
}
