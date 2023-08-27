using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility.DependencyInjection
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
                .Where(x => x.IsClass && !x.IsAbstract)
                .Where(x => x.GetCustomAttribute<AutoDiscoverScopedAttribute>() != null)
                .Where(x => x.GetCustomAttribute<DependencyIgnoreAttribute>() == null))
                services.AddScoped(t);

            return services;
        }
    }
}
