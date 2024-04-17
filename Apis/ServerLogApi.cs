using Danbo.Services;
using Danbo.Utility.DependencyInjection;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Danbo.Models.Config;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Danbo.Models.Jobs;

namespace Danbo.Apis;

[AutoDiscoverScoped]
internal class ServerLogApi
{
    public ServerLogApi(ScopedGuildId guildId, Database db, DiscordSocketClient client, ILogger<ServerLogApi> logger)
    {
        this.guildId = guildId;
        this.db = db;
        this.client = client;
        this.logger = logger;
    }

    public async Task OnMessageUpdate(ServerLogJob work)
    {
        var channelId = db
            .Select<StaffChannelConfig>()
            .FirstOrDefault()
            ?.ServerLogChannelId;

        if (channelId == null)
        {
            logger.LogWarning("ServerLogChannel not configured for guild {guildId}", guildId.Id);
            return;
        }

        var channel = client.GetChannel((ulong)channelId) as ITextChannel;

        var builder = new EmbedBuilder()
            .AddField("Old", work.CachedContent.NullIfEmpty() ?? "`not available in cache`")
            .WithTitle($"Message {work.Type.ToString().ToLower()} in {work.Channel.Mention}");

        switch (work.Type)
        {
            case MessageChangeType.Updated:
                builder.WithColor(Color.Gold)
                    .AddField("New", work.Message.Content)
                    .WithDescription($"[Jump to message](<{work.Message.GetJumpUrl()}>)");
                break;
            case MessageChangeType.Deleted:
                builder.WithColor(Color.Purple);
                break;
            default:
                throw new Exception($"Invalid value for {nameof(MessageChangeType)}");
        }

        if (work.CachedContent != null)
        {
            builder.AddField("Member", work.Message.Author.Mention)
                .WithAuthor(work.Message.Author);
        }

        await channel.SendMessageAsync(embed: builder.Build());
    }

    private readonly ScopedGuildId guildId;
    private readonly Database db;
    private readonly DiscordSocketClient client;
    private readonly ILogger<ServerLogApi> logger;
}
