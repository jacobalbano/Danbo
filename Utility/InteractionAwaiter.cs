using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility;

public interface IInteractionSignal
{
    string InteractionId { get; }

    /// <summary>
    /// Will be either <see cref="SocketMessageComponent"/> or <see cref="SocketModal"/>
    /// </summary>
    public IDiscordInteraction Interaction { get; }
}

public class InteractionAwaiter : IDisposable
{
    public InteractionAwaiter(SocketInteractionContext context)
    {
        this.context = context;
        context.Client.ModalSubmitted += Client_ModalSubmitted;
        context.Client.ButtonExecuted += Client_ComponentExecuted;
        context.Client.SelectMenuExecuted += Client_ComponentExecuted;
        Task.Run(Timeout, timeoutTokenSource.Token);
    }

    public async Task HandleInteractionsAsync(Func<IInteractionSignal, Task> handler)
    {
        try
        {
            while (active)
            {
                var signal = await tcs.Task;
                await handler(signal);
            }
        }
        catch (TaskCanceledException) { }
        catch { throw; }
    }

    public void Dispose()
    {
        if (disposed) throw new ObjectDisposedException(nameof(InteractionAwaiter));
        Stop();
        tcs.SetCanceled();
        disposed = true;
    }

    public IInteractionSignal Signal()
    {
        var result = new SignalImpl { InteractionId = Guid.NewGuid().ToString() };
        signals[result.InteractionId] = result;
        return result;
    }

    /// <summary>
    /// Call to manually stop listening for interactions.
    /// Also called automatically by <see cref="Dispose"/>
    /// </summary>
    public void Stop()
    {
        if (!active) return;
        context.Client.ModalSubmitted -= Client_ModalSubmitted;
        context.Client.ButtonExecuted -= Client_ComponentExecuted;
        context.Client.SelectMenuExecuted -= Client_ComponentExecuted;
        timeoutTokenSource.Cancel();
        signals.Clear();
        active = false;
    }

    private Task Client_ComponentExecuted(SocketMessageComponent arg) => TrySignal(arg, arg.Data.CustomId);
    private Task Client_ModalSubmitted(SocketModal arg) => TrySignal(arg, arg.Data.CustomId);

    private Task TrySignal(IDiscordInteraction arg, string customId)
    {
        if (!signals.TryGetValue(customId, out var signal))
            return Task.CompletedTask;

        signal.Interaction = arg;
        tcs.SetResult(signal);
        tcs = new();
        return Task.CompletedTask;
    }

    private async Task Timeout()
    {
        await Task.Delay(TimeSpan.FromMinutes(15));
        Stop();
    }

    private TaskCompletionSource<IInteractionSignal> tcs = new();
    private readonly CancellationTokenSource timeoutTokenSource = new();
    private readonly Dictionary<string, SignalImpl> signals = new();
    private readonly SocketInteractionContext context;
    private bool disposed, active = true;

    private class SignalImpl : IInteractionSignal
    {
        public string InteractionId { get; init; }
        public IDiscordInteraction Interaction { get; set; }
    }
}