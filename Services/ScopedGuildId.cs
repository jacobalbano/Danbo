using Danbo.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Services;

[AutoDiscoverScoped]
public class ScopedGuildId
{
    public ulong? Id { get; private set; }

    public void Initialize(ulong? guildId)
    {
        if (initialized)
            throw new Exception("Already initialized");
        initialized = true;
        Id = guildId;
    }

    private static int count = 0;
    public readonly int uuid = ++count;

    private bool initialized;
}
