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
    public bool IsValid => !disposed && !timedout && context.Interaction.IsValidToken;

    public InteractionAwaiter(SocketInteractionContext context)
    {
        this.context = context;
        context.Client.ModalSubmitted += Client_ModalSubmitted;
        context.Client.ButtonExecuted += Client_ComponentExecuted;
        context.Client.SelectMenuExecuted += Client_ComponentExecuted;
        Task.Run(Timeout, timeoutTokenSource.Token);
    }

    public Task<IInteractionSignal> WaitForSignal() => tcs.Task;

    public void Dispose()
    {
        if (disposed) throw new ObjectDisposedException(nameof(InteractionAwaiter));
        context.Client.ModalSubmitted -= Client_ModalSubmitted;
        context.Client.ButtonExecuted -= Client_ComponentExecuted;
        context.Client.SelectMenuExecuted -= Client_ComponentExecuted;
        timeoutTokenSource.Cancel();
        signals.Clear();
        disposed = true;
    }

    public IInteractionSignal Signal()
    {
        var result = new SignalImpl { InteractionId = Guid.NewGuid().ToString() };
        signals[result.InteractionId] = result;
        return result;
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
        tcs.SetCanceled();
        signals.Clear();
        timedout = true;
    }

    private TaskCompletionSource<IInteractionSignal> tcs = new();
    private readonly CancellationTokenSource timeoutTokenSource = new();
    private readonly Dictionary<string, SignalImpl> signals = new();
    private readonly SocketInteractionContext context;
    private bool disposed, timedout;

    private class SignalImpl : IInteractionSignal
    {
        public string InteractionId { get; init; }
        public IDiscordInteraction Interaction { get; set; }
    }
}