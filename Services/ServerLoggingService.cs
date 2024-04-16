using Danbo.Apis;
using Danbo.Models.Jobs;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Danbo.Services;

[AutoDiscoverSingletonService, ForceInitialization]
public class ServerLoggingService
{
    public ServerLoggingService(DiscordSocketClient client, ILogger<ServerLoggingService> logger, IServiceProvider services)
    {
        client.MessageDeleted += Client_MessageDeleted;
        client.MessageUpdated += Client_MessageUpdated;
        client.Connected += Client_Connected;
        this.client = client;
        this.logger = logger;
        this.services = services;
    }

    private Task Client_Connected()
    {
        client.Connected -= Client_Connected;
        Task.Run(LoggingTask);
        return Task.CompletedTask;
    }

    private async Task LoggingTask()
    {
        while (true)
        {
            var next = await queue.Reader.ReadAsync();
            using var scope = services.GuildScope(next.Channel.GuildId);
            var api = scope.ServiceProvider.GetRequiredService<ServerLogApi>();
            await api.OnMessageUpdate(next);
        }
    }

    private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> oldMessage, SocketMessage message, ISocketMessageChannel source)
    {
        if (source is not ITextChannel channel) return;
        if (message.Author.IsBot) return;
        queue.Writer.TryWrite(new(channel, MessageChangeType.Updated, message, oldMessage.Value?.Content));
    }

    private async Task Client_MessageDeleted(Cacheable<IMessage, ulong> oldMessage, Cacheable<IMessageChannel, ulong> source)
    {
        if (source.Value is not ITextChannel channel) return;
        queue.Writer.TryWrite(new(channel, MessageChangeType.Deleted, oldMessage.Value, oldMessage.Value?.Content));
    }

    private readonly DiscordSocketClient client;
    private readonly IServiceProvider services;
    private readonly ILogger<ServerLoggingService> logger;
    private readonly Channel<ServerLogJob> queue = Channel.CreateUnbounded<ServerLogJob>();
}
