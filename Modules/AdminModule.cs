using Discord;
using Discord.Interactions;
using Danbo.Models;
using Danbo.Utility;
using NodaTime;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Danbo.Apis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Danbo.Modules;

[RequireOwner]
[Group("admin", "Bot administration functions")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class AdminModule : ModuleBase
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

    [SlashCommand("export-stafflog", "Read through the staff log channel and collect/parse historical data")]
    [RequireOwner, DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task ExportStaffLog()
    {
        var defer = DeferAsync();

        await defer;
        await FollowupAsync("Generating export, please wait");

        var export = await staffApi.ExportStaffLog();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));
        using var stream = new MemoryStream(bytes);
        await Context.Channel.SendFileAsync(stream, "export.txt", $"{Context.User.Mention} Export completed. Upload the resulting file to the / `import-stafflog` command");
    }

    [SlashCommand("import-stafflog", "Upload a file containing infractions to be imported into user records")]
    [RequireOwner, DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task ImportStafflog(IAttachment attachment)
    {
        var defer = DeferAsync();
        var report = await staffApi.ImportStaffLog(attachment);

        await defer;
        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("Import completed")
            .WithDescription(report.ToString())
            .Build());
    }

    public AdminModule(StaffApi staffApi, ILogger<AdminModule> logger)
    {
        this.staffApi = staffApi;
        this.logger = logger;
    }

    private readonly StaffApi staffApi;
    private readonly ILogger<AdminModule> logger;
}
