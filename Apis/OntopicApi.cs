﻿using Danbo.Errors;
using Danbo.Models.Config;
using Danbo.Models.Jobs;
using Danbo.Services;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class OntopicApi
{
    public OntopicApi(Database database, SchedulerService scheduler)
    {
        this.database = database;
        this.scheduler = scheduler;
    }

    public async Task RemoveOntopicFromUser(IGuildUser user)
    {
        if (GetOntopicRoleId() is not ulong roleId)
            throw new FollowupError("Ontopic is not configured.");

        await user.RemoveRoleAsync(roleId, new RequestOptions { AuditLogReason = "Role expired" });
        using var s = database.BeginSession();
        foreach (var job in s.Select<OntopicExpirationJob>()
            .Where(x => x.UserId == user.Id)
            .ToEnumerable())
            s.Delete(job);
    }

    public async Task AddOntopicToUser(IGuildUser user, Instant expiration)
    {
        if (GetOntopicRoleId() is not ulong roleId)
            throw new FollowupError("Ontopic is not configured.");

        if (user.RoleIds.Contains(roleId))
            throw new FollowupError("You already have the Ontopic role");

        using var s = database.BeginSession();
        s.Insert(new OntopicExpirationJob
        {
            Expiration = expiration,
            UserId = user.Id,
            JobHandle = scheduler.AddJob(expiration, () => RemoveOntopicFromUser(user)),
        });

        await user.AddRoleAsync(roleId, new RequestOptions { AuditLogReason = "User requested role" });
    }

    public ulong? GetOntopicRoleId() => GetConfig()?.RoleId;

    public void SetOntopicRoleId(ulong? roleId)
    {
        var insert = (GetConfig() ?? new()) with { RoleId = roleId ?? 0 };
        using var s = database.BeginSession();
        if (roleId == null) s.Delete(insert);
        else s.InsertOrUpdate(insert);
    }

    private OntopicConfig GetConfig() => database
        .Select<OntopicConfig>()
        .FirstOrDefault();

    private readonly Database database;
    private readonly SchedulerService scheduler;
}