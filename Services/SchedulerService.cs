using Danbo.Utility;
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
    [AutoDiscoverSingletonService]
    public class SchedulerService
    {
        public SchedulerService(ILogger<SchedulerService> logger)
        {
            this.logger = logger;
            Task.Run(Spin);
        }

        public IDisposable BeginTransaction()
        {
            transactions++;
            return new Transaction(this);
        }

        public ulong AddJob(Instant instant, Func<Task> callback)
        {
            var next = Interlocked.Increment(ref nextJobId);
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

                var processEvents = jobs.ToDictionary(x => x.Key, x => x.Value);
                var now = SystemClock.Instance.GetCurrentInstant();
                foreach (var (k, v) in processEvents)
                {
                    if (v.Time < now)
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
        }

        private async Task NextTick()
        {
            await Task.WhenAny(
                signal.WaitAsync(),
                Task.Delay(ApproachNextEvent())
            );

            if (signal.CurrentCount != 0)
                await signal.WaitAsync();
        }

        private void RecalculateTick()
        {
            if (transactions == 0)
                signal.Release();
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
            if (half < Duration.FromMinutes(1))
                return TimeSpan.FromSeconds(5);
            else
                return half.ToTimeSpan();
        }

        private class Transaction : IDisposable
        {
            public SchedulerService Owner { get; }
            public Transaction(SchedulerService owner) => Owner = owner;

            void IDisposable.Dispose()
            {
                Owner.transactions--;
                Owner.RecalculateTick();
            }
        }

        private readonly ILogger<SchedulerService> logger;
        private readonly ConcurrentDictionary<ulong, Job> jobs = new();
        private readonly SemaphoreSlim signal = new(0, 1);

        private ulong nextJobId = 0;
        private byte transactions = 0;

        private record class Job(ulong Id, Instant Time, Func<Task> Action);
    }
}
