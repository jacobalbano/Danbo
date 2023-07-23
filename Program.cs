using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Danbo.Modules;
using Danbo.TypeConverters;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Logging;
using Danbo.Utility;
using Danbo.Models;
using Danbo.Services;

namespace Danbo;

public class Program
{
    public static ConfigFile BotConfig { get; } = ConfigFile.Prepare();

    static void Main(string[] args)
    {
        if (args.Any())
            Directory.SetCurrentDirectory(args[0]);

        RunAsync().GetAwaiter().GetResult();
    }

    static async Task RunAsync()
    {
        using var services = ConfigureServices();

        var client = services.GetRequiredService<DiscordSocketClient>();
        var handler = services.GetRequiredService<CommandHandlerService>();

        await handler.Initialize();
        await client.LoginAsync(TokenType.Bot, BotConfig.Token);
        await client.StartAsync();

        await Task.Delay(Timeout.Infinite);
    }

    static ServiceProvider ConfigureServices() =>
        new ServiceCollection()
        .DiscoverTaggedSingletons()
        .DiscoverTaggedInterfaces()
        .DiscoverTaggedScopedServices()
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton(new DiscordSocketConfig {
            LogGatewayIntentWarnings = false,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
#if !DEBUG
            LogLevel = LogSeverity.Error
#endif
        })
        .AddSingleton(new InteractionServiceConfig { AutoServiceScopes = false })
        .AddSingleton<InteractionService>()
        .AddLogging(x => ConfigureLogging(x))
        .BuildServiceProvider()
        .DiscoverAndInitialize();

    private static ILoggingBuilder ConfigureLogging(ILoggingBuilder x)
    {
        return x.AddSerilog(new LoggerConfiguration()
            .WriteTo.File("logs/danbo.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: null,
                shared: true
            )
            .WriteTo.Console()
            .CreateLogger());
    }
}
