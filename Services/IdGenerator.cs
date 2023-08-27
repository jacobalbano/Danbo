using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Services;

[AutoDiscoverSingletonService]
public class IdGenerator
{
    public ulong Next() => (ulong) impl.CreateId();

    private readonly IdGen.IdGenerator impl = new(0);
}
