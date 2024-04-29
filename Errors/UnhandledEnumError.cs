using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Errors;

public static class UnhandledEnumException
{
    public static UnhandledEnumException<TEnum> From<TEnum>(TEnum value)
        where TEnum : Enum => new(value);
}

public class UnhandledEnumException<TEnum> : Exception
    where TEnum : Enum
{
    public UnhandledEnumException(TEnum value)
        : base($"Unhandled value '{value}' for {typeof(TEnum).Name}")
    {
    }
}
