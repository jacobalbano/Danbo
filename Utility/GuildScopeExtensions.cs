using Danbo.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal static class GuildScopeExtensions
{
    public static IServiceScope GuildScope(this IServiceProvider services, ulong guildId)
    {
        var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ScopedGuildId>()
            .Initialize(guildId);
        return scope;
    }
}
