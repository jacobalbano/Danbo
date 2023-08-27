using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility
{
    public static class LiteDbUtility
    {
        public static T FirstOrDefault<T>(this ILiteQueryable<T> self, Expression<Func<T, bool>> predicate)
        {
            return self.Where(predicate).FirstOrDefault();
        }

        public static bool Any<T>(this ILiteQueryable<T> self)
        {
            return self.Count() > 0;
        }

        public static HashSet<T> ToHashSet<T>(this ILiteQueryable<T> self) => self.ToEnumerable().ToHashSet();
    }
}
