using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models;

public record class PinnedMessage : ModelBase
{
    public ulong DiscordChannelId { get; init; }
    public ulong DiscordMessageId { get; init; }

    [Indexed]
    public string UniqueName { get; init; }
}
