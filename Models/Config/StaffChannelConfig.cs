using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models.Config;

public record class StaffChannelConfig : ModelBase
{
    public ulong? StaffLogChannelId { get; init; }
    public ulong? ServerLogChannelId { get; init; }
}
