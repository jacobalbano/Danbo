using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models;

public record class AuditEntry : ModelBase
{
    public ulong? User { get; init; }

    public string Message { get; init; }

    public string? DetailMessage { get; init; }

    public ulong? DetailId { get; init; }

    public DetailIdType DetailType { get; init; }
}

public enum DetailIdType
{
    None,
    Channel,
    Role,
    User,
}
