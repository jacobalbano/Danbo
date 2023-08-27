using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
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

    public void Initialize(ulong? value)
    {
        if (initialized)
            throw new Exception("Already initialized");
        initialized = true;
        Id = value;
    }

    private bool initialized;
}
