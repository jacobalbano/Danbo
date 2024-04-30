using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models;

public record class AnalysisBlacklist : ModelBase
{
    [Indexed]
    public ulong ChannelId { get; init; }
}
