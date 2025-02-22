﻿using Danbo.Apis;
using Danbo.Models.Jobs;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        client.MessagesBulkDeleted += Client_MessagesBulkDeleted;
        client.UserLeft += Client_UserLeft;
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
            await Task.Delay(TimeSpan.FromSeconds(1) / 6);
            var next = await queue.Reader.ReadAsync();

            using var scope = services.GuildScope(next.GuildId);
            var api = scope.ServiceProvider.GetRequiredService<ServerLogApi>();
            try
            {
                await api.ProcessEvent(next);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error in server logging queue");
            }
        }
    }

    private async Task Client_MessageUpdated(Cacheable<IMessage, ulong> oldMessage, SocketMessage message, ISocketMessageChannel source)
    {
        if (source is not ITextChannel channel) return;
        if (message.Author.Id == client.CurrentUser.Id) return;
        queue.Writer.TryWrite(new MessageUpdateLogJob(channel, message, oldMessage.Value?.Content));
    }

    private async Task Client_MessageDeleted(Cacheable<IMessage, ulong> oldMessage, Cacheable<IMessageChannel, ulong> source)
    {
        if (source.Value is not ITextChannel channel) return;
        if (oldMessage.Value?.Author.Id == client.CurrentUser.Id) return;
        queue.Writer.TryWrite(new MessageDeleteLogJob(channel, oldMessage.Value));
    }

    private async Task Client_MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> oldMessages, Cacheable<IMessageChannel, ulong> source)
    {
        if (source.Value is not ITextChannel channel) return;

        foreach (var message in oldMessages.OrderBy(x => x.Value?.Timestamp))
        {
            if (message.Value?.Author.Id == client.CurrentUser.Id) return;
            queue.Writer.TryWrite(new MessageDeleteLogJob(channel, message.Value));
        }
    }

    private async Task Client_UserLeft(SocketGuild guild, SocketUser user)
    {
        queue.Writer.TryWrite(new UserLeftLogJob(guild.Id, user));
    }

    private readonly DiscordSocketClient client;
    private readonly IServiceProvider services;
    private readonly ILogger logger;
    private readonly Channel<ServerLogJob> queue = Channel.CreateUnbounded<ServerLogJob>();
}
