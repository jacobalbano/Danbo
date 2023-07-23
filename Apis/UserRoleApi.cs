using Danbo.Models;
using Danbo.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class UserRoleApi
{
    public IReadOnlyList<ulong> GetUserRoles()
    {
        return database.Select<UserRole>()
            .ToEnumerable()
            .Select(x => x.Id)
            .ToList();
    }

    public bool EnableUserRole(ulong roleId)
    {
        using var s = database.BeginSession();
        var dbRole = s.Select<UserRole>()
            .FirstOrDefault(x => x.Id == roleId);

        if (dbRole == null)
            s.Insert(new UserRole { Id = roleId });
        return dbRole == null;
    }

    public bool DisableUserRole(ulong roleId)
    {
        using var s = database.BeginSession();
        var dbRole = s.Select<UserRole>()
            .FirstOrDefault(x => x.Id == roleId);

        if (dbRole != null)
            s.Delete(dbRole);
        return dbRole != null;
    }

    public UserRoleApi(Database database)
    {
        this.database = database;
    }

    private readonly Database database;
}
