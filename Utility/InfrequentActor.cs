using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility;

public class InfrequentActor
{
    public Duration DoNoMoreFrequentlyThan { get; }

    public InfrequentActor(Duration doNoMoreFrequentlyThan)
    {
        DoNoMoreFrequentlyThan = doNoMoreFrequentlyThan;
        nextInstant = SystemClock.Instance.GetCurrentInstant();
    }

    public Task Act(Func<Task> action)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        if (now < nextInstant || action == null)
            return Task.CompletedTask;

        nextInstant = now + DoNoMoreFrequentlyThan;
        return action();
    }

    public void Act(Action action)
    {
        Act(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    private Instant nextInstant;
}