using Discord;
using Discord.WebSocket;
using Danbo.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.ReactionHandlers;

[AutoDiscoverImplementations]
public interface IReactionHandler
{
    public IEmote Emote { get; }

    public Task OnReactionAdded(IUserMessage message, IMessageChannel channel, SocketReaction reaction, DiscordSocketClient discord);
    public Task OnReactionRemoved(IUserMessage message, IMessageChannel channel, SocketReaction reaction, DiscordSocketClient discord);

    public static IEnumerable<IReactionHandler> DiscoverReactionHandlers() => typeof(IReactionHandler).Assembly
        .GetTypes()
        .Where(typeof(IReactionHandler).IsAssignableFrom)
        .Where(x => !x.IsInterface && !x.IsAbstract)
        .Select(x => (IReactionHandler)Activator.CreateInstance(x)!);
}