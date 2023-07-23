using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models;

public record class Tag : ModelBase
{
    [Indexed]
    public string Name { get; init; }

    public string Text { get; init; }
}
