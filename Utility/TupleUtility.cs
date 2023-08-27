using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class TupleUtility
{
    public static void Deconstruct<T1, T2>(this IGrouping<T1, T2> group, out T1 key, out IEnumerable<T2> elements)
    {
        key = group.Key;
        elements = group;
    }
}
