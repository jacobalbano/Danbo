using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Services
{
    [AutoDiscoverSingletonService, ForceInitialization]
    public class SchedulerService
    {
        public SchedulerService(ILogger<SchedulerService> logger, IdGenerator idGen)
        {
            this.logger = logger;
            this.idGen = idGen;
            Task.Run(Spin);
        }

        public ulong AddJob(Instant instant, Func<Task> callback)
        {
            var next = idGen.Next();
            if (!jobs.TryAdd(next, new Job(next, instant, callback)))
                throw new Exception("Failed to add scheduled job");

            RecalculateTick();
            return next;
        }

        public bool UpdateJob(ulong handle, Instant instant, Func<Task> callback)
        {
            if (!jobs.TryGetValue(handle, out var job))
                return false;

            jobs[handle] = new Job(handle, instant, callback);
            RecalculateTick();
            return true;
        }

        public bool RemoveJob(ulong handle)
        {
            var result = jobs.Remove(handle, out _);
            RecalculateTick();
            return result;
        }

        private async Task Spin()
        {
            while (true)
            {
                await NextTick();
                var now = SystemClock.Instance.GetCurrentInstant();
                var processEvents = jobs
                    .Where(x => x.Value.Time < now)
                    .ToDictionary(x => x.Key, x => x.Value);

                foreach (var (k, v) in processEvents)
                {
                    try
                    {
                        await v.Action();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error when executing scheduled job");
                    }
                    finally
                    {
                        jobs.TryRemove(k, out _);
                    }
                }
            }
        }

        private async Task NextTick()
        {
            try
            {
                var next = ApproachNextEvent();
                logger.LogTrace("{nextTick}", next);
                await Task.Delay(next, signal.Token);
            }
            catch (TaskCanceledException)
            {
                logger.LogTrace("Got signal, recalculating delay");
                signal = new();
            }
        }

        private void RecalculateTick()
        {
            if (transactions == 0)
                signal.Cancel();
        }

        private TimeSpan ApproachNextEvent()
        {
            if (!jobs.Any())
                return Timeout.InfiniteTimeSpan;

            var now = SystemClock.Instance.GetCurrentInstant();
            var earliest = jobs.Values
                .MinBy(x => x.Time);

            if (earliest!.Time < now)
                return TimeSpan.Zero;

            var half = (earliest.Time - now) / 2;
            if (half < Duration.FromSeconds(1))
                return TimeSpan.FromMilliseconds(600);
            else
                return half.ToTimeSpan();
        }

        private readonly ILogger logger;
        private readonly IdGenerator idGen;
        private readonly ConcurrentDictionary<ulong, Job> jobs = new();
        private CancellationTokenSource signal = new();

        private byte transactions = 0;

        private record class Job(ulong Id, Instant Time, Func<Task> Action);
    }
}
