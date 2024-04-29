using Danbo.Models;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class AuditApi
{
    public void Log(string message, ulong? userId = null, ulong? detailId = null, DetailIdType detailIdType = DetailIdType.None, string detailMessage = null)
    {
        using var s = database.BeginSession();
        s.Insert(new AuditEntry
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            User = userId,
            DetailId = detailId,
            DetailType = detailIdType,
            DetailMessage = detailMessage
        });
    }

    public void Log<TDetailTuple>(string message, ulong? userId = null, ulong? detailId = null, DetailIdType detailIdType = DetailIdType.None, TDetailTuple detailObject = default)
        where TDetailTuple : IStructuralEquatable, IStructuralComparable, IComparable
    {
        using var s = database.BeginSession();
        s.Insert(new AuditEntry
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            User = userId,
            DetailId = detailId,
            DetailType = detailIdType,
            DetailMessage = detailObject?.ToString() ?? null
        });
    }

    public void Search()
    {
        throw new NotImplementedException();
    }

    public AuditApi(GuildDb database)
    {
        this.database = database;
    }

    private readonly GuildDb database;
}
