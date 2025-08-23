using Danbo.Services;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility;

[AutoDiscoverScoped]
public class CommandUtility
{
    public CommandUtility(DiscordSocketClient discord, ScopedGuildId guildId)
    {
        if (guildId.Id is ulong id)
        {
            guild = discord.GetGuild(id);
            commandIdsTask = Task.Run(async () => (await guild.GetApplicationCommandsAsync())
                .ToDictionary(x => x.Name));
        }
    }

    public Task<string> GetCommandMention(SlashCommandInfo item)
    {
        return GetCommandMention(item.Module.IsSlashGroup && !item.IgnoreGroupNames
            ? item.ToString().Split(' ')
            : new[] { item.Name });
    }

    public Task<string> GetCommandMention(MethodInfo methodInfo)
    {
        var methodAttr = methodInfo.GetCustomAttribute<SlashCommandAttribute>();
        if (methodAttr.IgnoreGroupNames)
            return GetCommandMention(new[] { methodAttr.Name });

        var result = new List<string> { methodAttr.Name };
        for (var parent = methodInfo.DeclaringType; parent != null; parent = parent.DeclaringType)
        {
            var classAttr = methodInfo.DeclaringType.GetCustomAttribute<GroupAttribute>();
            if (!string.IsNullOrEmpty(classAttr.Name))
                result.Add(classAttr.Name);
        }

        result.Reverse();
        return GetCommandMention(result);
    }

    private async Task<string> GetCommandMention(IReadOnlyList<string> commandParts)
    {
        var dict = await commandIdsTask;
        if (!dict.TryGetValue(commandParts[0], out var command))
            throw new Exception($"No command {commandParts[0]} was found");

        return $"</{string.Join(' ', commandParts)}:{command.Id}>";
    }

    private SocketGuild guild;
    private readonly Task<Dictionary<string, SocketApplicationCommand>> commandIdsTask;
}

public class methodof<T>
{
    public methodof(T func)
    {
        Delegate del = (Delegate)(object)func;
        this.method = del.Method;
    }

    public static implicit operator methodof<T>(T methodof)
    {
        return new methodof<T>(methodof);
    }

    public static implicit operator MethodInfo(methodof<T> methodof)
    {
        return methodof.method;
    }

    private readonly MethodInfo method;
}