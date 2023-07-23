using Discord;
using Discord.Interactions;
using Danbo.Models;
using Danbo.Utility;
using NodaTime;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Danbo.Modules;

[RequireOwner]
[Group("admin", "Bot administration functions")]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("run-diagnostics", "Run various diagnostics to see how the bot can be expected to perform", runMode: RunMode.Async)]
    public async Task RunDiagnostics()
    {
        var defer = DeferAsync(ephemeral: true);
        var builder = new EmbedBuilder();

        var proc = Process.GetCurrentProcess();
        var nativeMem = proc.PrivateMemorySize64;
        var gcMem = GC.GetTotalMemory(forceFullCollection: false);
        static string format(long l) => $"{BytesFormatter.ToSize(l, BytesFormatter.SizeUnits.MB)}mb";

        var gitHash = Assembly
            .GetEntryAssembly()?
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

        builder.AddField("Native memory usage", format(nativeMem), inline: true);
        builder.AddField("GC memory usage", format(gcMem), inline: true);
        builder.AddField("Total memory usage", format(nativeMem + gcMem));

        if (gitHash != null)
            builder.AddField("Commit", gitHash);

        builder.WithFooter($"Bot uptime: {Duration.FromTimeSpan(DateTime.UtcNow - proc.StartTime.ToUniversalTime())}");

        await defer;
        await FollowupAsync(embed: builder.Build(), ephemeral: true);
    }
}
