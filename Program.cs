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
using IdGen;
using Danbo.Utility.DependencyInjection;
using Serilog.Configuration;
using Discord.Rest;

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
        var logger = services.GetRequiredService<ILogger<Program>>();

        await handler.Initialize();

        while (true)
        {
            var tcs = new TaskCompletionSource();
            await client.LoginAsync(TokenType.Bot, BotConfig.Token);
            await client.StartAsync();

            client.Disconnected += cancelAndReconnect;

            await tcs.Task;
            logger.LogWarning("Bot disconnected, attempting to restart");
            await Task.Delay(5000);

            Task cancelAndReconnect(Exception ex)
            {
                if (ex is TaskCanceledException)
                {
                    tcs.SetResult();
                    client.Disconnected -= cancelAndReconnect;
                    logger.LogInformation("Disconnect handler");
                }
                return Task.CompletedTask;
            }
        }
    }

    static ServiceProvider ConfigureServices() => new ServiceCollection()
        .DiscoverTaggedSingletons()
        .DiscoverTaggedInterfaces()
        .DiscoverTaggedScopedServices()
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton(new DiscordSocketConfig {
            LogGatewayIntentWarnings = false,
            MessageCacheSize = 4096,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent | GatewayIntents.GuildScheduledEvents,
#if DEBUG
            LogLevel = LogSeverity.Debug
#else
            LogLevel = LogSeverity.Error
#endif
        })
        .AddSingleton(p => new InteractionService(p.GetRequiredService<DiscordSocketClient>(), new() { AutoServiceScopes = false }))
        .AddSingleton(new HttpClient())
        .AddLogging(x => ConfigureLogging(x))
        .BuildServiceProvider()
        .DiscoverAndInitialize();

    private static ILoggingBuilder ConfigureLogging(ILoggingBuilder x) => x.AddSerilog(new LoggerConfiguration()
#if DEBUG
        .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
#endif
        .WriteTo.File("logs/danbo.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: null,
            shared: true
        )
        .WriteTo.Console()
        .CreateLogger());
}
