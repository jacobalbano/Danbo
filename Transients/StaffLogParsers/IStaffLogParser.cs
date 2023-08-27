using Danbo.Models;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Transients.StaffLogParsers;

[AutoDiscoverImplementations]
public interface IStaffLogParser
{
    ulong Id { get; }

    IEnumerable<Infraction> ParseInfractions(IMessage message);
}
