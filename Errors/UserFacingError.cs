using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Errors;

public class UserFacingError : Exception
{
    public UserFacingError(string message) : base(message)
    {
    }
}
