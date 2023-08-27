using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Danbo.Utility;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Danbo.Utility.DependencyInjection;

namespace Danbo.Services
{
    [AutoDiscoverSingletonService, ForceInitialization]
    public class AppLoggingService
    {
        public AppLoggingService(DiscordSocketClient client, InteractionService commands, ILogger<AppLoggingService> logger)
        {
            client.Log += (msg) =>
            {
                logger.Log(GetLogLevel(msg), "[Client] {msg}", msg);
                return Task.CompletedTask;
            };

            commands.Log += (msg) =>
            {
                logger.Log(GetLogLevel(msg), "[Commands] {msg}", msg);
                return Task.CompletedTask;
            };
        }

        private static LogLevel GetLogLevel(LogMessage msg)
        {
            return msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error when msg.Exception is TaskCanceledException => LogLevel.Debug,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            };
        }
    }
}