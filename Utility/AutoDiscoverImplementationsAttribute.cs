using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class AutoDiscoverImplementationsAttribute : Attribute
    {
    }

    public static class AutoDiscoverImplementationsAttributeExtensions
    {
        public static IServiceCollection DiscoverTaggedInterfaces(this IServiceCollection services)
        {
            foreach (var t in typeof(AutoDiscoverImplementationsAttribute)
                .Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract))
            {
                foreach (var iface in t.GetInterfaces()
                    .Where(x => x.GetCustomAttribute<AutoDiscoverImplementationsAttribute>() != null))
                    services.AddTransient(iface, t);
            }

            return services;
        }
    }
}
