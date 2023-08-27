using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

public abstract class ModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    protected async Task WrapDeferred(Func<string> task)
    {
        await WrapDeferred(() => Task.FromResult(task()));
    }

    protected async Task WrapDeferred(Func<Task<string>> task)
    {
        var defer = DeferAsync(ephemeral: true);
        var message = await task();
        await defer;
        await FollowupAsync(ephemeral: true, embed: new EmbedBuilder()
            .WithDescription(message)
            .Build());
    }
}
