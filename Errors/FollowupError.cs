using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Errors;

internal class FollowupError : Exception
{
    public FollowupError(string message) : base(message)
    {
    }
}
