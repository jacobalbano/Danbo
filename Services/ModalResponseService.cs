using Danbo.Utility;
using Discord;
using Discord.WebSocket;
using NodaTime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Services;

[AutoDiscoverSingletonService]
public class ModalResponseService
{
    public ModalResponseService(DiscordSocketClient discord, SchedulerService scheduler)
    {
        discord.ModalSubmitted += HandleModal;
        this.scheduler = scheduler;
    }

    private Task HandleModal(SocketModal arg)
    {
        if (!pending.TryRemove(arg.Data.CustomId, out var job))
            throw new Exception("Tried to submit an expired modal");

        job.TaskCompletion.SetResult(arg);
        scheduler.RemoveJob(job.SchedulerHandle);
        return Task.CompletedTask;
    }

    public Task<SocketModal> WaitForModalAsync(Modal modal)
    {
        var tcs = new TaskCompletionSource<SocketModal>();
        var expiration = SystemClock.Instance.GetCurrentInstant() + Duration.FromMinutes(15);
        pending.TryAdd(modal.CustomId, new PendingJob(scheduler.AddJob(expiration, () =>
        {
            if (pending.TryRemove(modal.CustomId, out var cancelJob))
                cancelJob.TaskCompletion.SetCanceled();
            return Task.CompletedTask;
        }), tcs));
        
        return tcs.Task;
    }

    private readonly ConcurrentDictionary<string, PendingJob> pending = new();
    private readonly SchedulerService scheduler;

    private record class PendingJob(ulong SchedulerHandle, TaskCompletionSource<SocketModal> TaskCompletion);
}
