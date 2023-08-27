using Discord;
using Discord.Interactions;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using Danbo.Utility;
using Danbo.Transients.ReactionHandlers;
using Danbo.Utility.DependencyInjection;

namespace Danbo.Modules;

[AutoDiscoverSingletonService, ForceInitialization]
public class ReactionWatcherService
{
    private Task Discord_ReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (!handlers.TryGetValue(reaction.Emote, out var handler))
            return Task.CompletedTask;

        return Task.Run(async () =>
        {
            var msgVal = await message.GetOrDownloadAsync();
            var chnlVal = await channel.GetOrDownloadAsync();
            return handler.OnReactionRemoved(msgVal, chnlVal, reaction, discord);
        });
    }

    private Task Discord_ReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (!handlers.TryGetValue(reaction.Emote, out var handler))
            return Task.CompletedTask;

        Task.Run(async () =>
        {
            var msgVal = await message.GetOrDownloadAsync();
            var chnlVal = await channel.GetOrDownloadAsync();
            return handler.OnReactionAdded(msgVal, chnlVal, reaction, discord);
        });

        return Task.CompletedTask;
    }

    public ReactionWatcherService(DiscordSocketClient discord, IEnumerable<IReactionHandler> handlers)
    {
        this.discord = discord;
        discord.ReactionAdded += Discord_ReactionAdded;
        discord.ReactionRemoved += Discord_ReactionRemoved;
        this.handlers = handlers.ToDictionary(x => x.Emote);
    }

    private readonly DiscordSocketClient discord;
    private readonly IReadOnlyDictionary<IEmote, IReactionHandler> handlers;
}