using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverSingletonService, ForceInitialization]
public class GuildEventApi
{
    public GuildEventApi(DiscordSocketClient discord)
    {
        this.discord = discord;
    }

    private readonly DiscordSocketClient discord;
}
