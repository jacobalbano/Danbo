using Danbo.Errors;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules.Parameters;

public enum DurationUnit
{
    Minutes,
    Hours,
    Days
}

public static class DurationUnitExtensions
{
    public static Duration ToDuration(this DurationUnit unit, int amount)
    {
        return unit switch
        {
            DurationUnit.Minutes => Duration.FromMinutes(amount),
            DurationUnit.Hours => Duration.FromHours(amount),
            DurationUnit.Days => Duration.FromDays(amount),
            _ => throw new FollowupError("Invalid unit parameter")
        };
    }
}