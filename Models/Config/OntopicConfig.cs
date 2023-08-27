using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models.Config;

public record class OntopicConfig : ModelBase
{
    public ulong RoleId { get; init; }
}
