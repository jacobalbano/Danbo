using Danbo.Models;
using Danbo.Models.Config;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.WebSocket;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class RapsheetApi
{
    public Infraction Add(ulong moderatorId, InfractionType infractionType, ulong userId, string message)
    {
        using var s = db.BeginSession();
        return s.Insert(new Infraction
        {
            ModeratorId = moderatorId,
            InfractionInstant = SystemClock.Instance.GetCurrentInstant(),
            Message = message,
            Type = infractionType,
            UserId = userId,
        });
    }

    public IEnumerable<Infraction> SearchInfractions(InfractionType? infractionType, string keyword)
    {
        using var s = db.BeginSession();
        return s.Select<Infraction>()
            .Where(x => (infractionType != null && x.Type == infractionType) || (!string.IsNullOrEmpty(keyword) && x.Message.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)))
            .ToEnumerable();
    }

    public IEnumerable<Infraction> GetInfractionsForUser(IUser user)
    {
        using var s = db.BeginSession();
        return s.Select<Infraction>()
            .Where(x => x.UserId == user.Id)
            .ToEnumerable();
    }

    public Infraction Remove(ulong userId, Guid infractionKey)
    {
        using var s = db.BeginSession();
        var toDelete = s.Select<Infraction>()
            .FirstOrDefault(x => x.Key == infractionKey && x.UserId == userId);
        if (toDelete != null)
            s.Delete(toDelete);

        return toDelete;
    }

    public RapsheetApi(Database db)
    {
        this.db = db;
    }

    private readonly Database db;
}
