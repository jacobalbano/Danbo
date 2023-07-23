using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AutoDiscoverSingletonServiceAttribute : Attribute
    {
    }

    public static class AutoDiscoverSingletonServiceAttributeExtensions
    {
        public static IServiceCollection DiscoverTaggedSingletons(this IServiceCollection services)
        {
            foreach (var t in typeof(AutoDiscoverSingletonServiceAttribute)
                .Assembly.GetExportedTypes()
                .Where(x => x.IsClass && !x.IsAbstract && x.GetCustomAttribute<AutoDiscoverSingletonServiceAttribute>() != null))
                services.AddSingleton(t);

            return services;
        }
    }
}
