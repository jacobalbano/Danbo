using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class AutoDiscoverScopedAttribute : Attribute
    {
    }

    public static class AutoDiscoverScopedAttributeExtensions
    {
        public static IServiceCollection DiscoverTaggedScopedServices(this IServiceCollection services)
        {
            foreach (var t in typeof(AutoDiscoverImplementationsAttribute)
                .Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && x.GetCustomAttribute<AutoDiscoverScopedAttribute>() != null))
                services.AddScoped(t);

            return services;
        }
    }
}
