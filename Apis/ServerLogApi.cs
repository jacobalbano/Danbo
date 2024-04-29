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
    public ServerLogApi(ScopedGuildId guildId, GuildDb db, DiscordSocketClient client, ILogger<ServerLogApi> logger)
    {
        this.guildId = guildId;
        this.db = db;
        this.client = client;
        this.logger = logger;
    }

    public async Task ProcessEvent(ServerLogJob work)
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
        var mainEmbed = new EmbedBuilder();
        var builders = new List<EmbedBuilder> { mainEmbed };
        var color = Color.Default;

        if (work is MessageChangeLogJob change)
        {
            builders.Add(new EmbedBuilder()
                .WithTitle("Old value")
                .WithDescription(change.CachedContent.NullIfEmpty() ?? "`not available in cache`"));
        }

        switch (work)
        {
            case MessageUpdateLogJob update:
                if (update.Message.Content == update.CachedContent)
                    return;

                color = Color.Gold;
                mainEmbed
                    .WithAuthor(update.Message.Author)
                    .WithDescription($"[Jump to message](<{update.Message.GetJumpUrl()}>)")
                    .WithTitle($"Message updated in {update.Channel.Mention}")
                    .AddField("Member", update.Message.Author.Mention);

                builders.Add(new EmbedBuilder()
                    .WithTitle("New value")
                    .WithDescription(update.Message.Content));
                break;
            case MessageDeleteLogJob delete:
                color = Color.Purple;
                mainEmbed.WithTitle($"Message deleted in {delete.Channel.Mention}");
                if (delete.Message != null)
                    mainEmbed
                        .WithAuthor(delete.Message.Author)
                        .AddField("Member", delete.Message.Author.Mention);
                break;
            case UserLeftLogJob userLeft:
                color = Color.DarkTeal;
                mainEmbed.WithTitle("User left")
                    .AddField("Member", userLeft.User.Mention);
                break;
            default:
                logger.LogWarning($"Unhandled {nameof(ServerLogJob)} subclass {{jobTypeName}}", work.GetType().Name);
                return;
        }

        await channel.SendMessageAsync(embeds: builders
            .Select(x => x.WithColor(color).Build())
            .ToArray());
    }

    private readonly ScopedGuildId guildId;
    private readonly GuildDb db;
    private readonly DiscordSocketClient client;
    private readonly ILogger logger;
}
