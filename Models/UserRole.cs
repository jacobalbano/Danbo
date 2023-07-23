using Danbo.Models;

namespace Danbo.Models;

public record class UserRole : ModelBase
{
    [Indexed]
    public ulong Id { get; init; }
}