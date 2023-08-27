using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility.DependencyInjection
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class AutoDiscoverImplementationsAttribute : Attribute
    {
        public bool Transient { get; init; } = true;
    }

    public static class AutoDiscoverImplementationsAttributeExtensions
    {
        public static IServiceCollection DiscoverTaggedInterfaces(this IServiceCollection services)
        {
            foreach (var t in typeof(AutoDiscoverImplementationsAttribute)
                .Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract)
                .Where(x => x.GetCustomAttribute<DependencyIgnoreAttribute>() == null))
            {
                foreach (var iface in t.GetInterfaces())
                {
                    var attr = iface.GetCustomAttribute<AutoDiscoverImplementationsAttribute>();
                    if (attr == null) continue;
                    if (attr.Transient)
                        services.AddTransient(iface, t);
                    else
                        services.AddScoped(iface, t);
                }
            }

            return services;
        }
    }
}
