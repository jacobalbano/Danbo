using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility.DependencyInjection;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class DependencyIgnoreAttribute : Attribute
{
}
