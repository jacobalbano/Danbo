using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal static class DiscordExtensions
{
    public static async Task<IGuildUser> GetUserAsync(this SocketGuild guild, ulong userId)
    {
        var user = guild.GetUser(userId);
        if (user != null) return user;
        await guild.DownloadUsersAsync();
        return guild.GetUser(userId);
    }
}
