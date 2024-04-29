using Danbo.Services;
using Danbo.Utility.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo;

[AutoDiscoverScoped]
public class GuildDb : Database
{
    public GuildDb(ScopedGuildId gid) : base(gid, "Data") { }
};
