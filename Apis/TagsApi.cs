using Danbo.Models;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using NodaTime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class TagsApi
{
    public void SetTag(string tagName, string content)
    {
        var tag = database
            .Select<Tag>()
            .FirstOrDefault(x => x.Name == tagName)
            ?? new Tag { Name = tagName };

        using var s = database.BeginSession();
        if (content == null) s.Delete(tag);
        else s.InsertOrUpdate(tag with { Text = content });

        foreach (var item in cooldownExpires.Where(x => x.Key.TagName == tagName).ToList())
            cooldownExpires.TryRemove(item);
    }

    public string GetTag(string tagName)
    {
        return database
            .Select<Tag>()
            .FirstOrDefault(x => x.Name == tagName)
            ?.Text;
    }

    public IReadOnlyList<string> GetTags()
    {
        return database
            .Select<Tag>()
            .Select(x => x.Name)
            .ToEnumerable()
            .OrderBy(x => x)
            .ToList();
    }

    public bool TryTakeWithCooldown(ulong channelId, string tagName)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        var key = new TagCooldown(channelId, tagName);
        bool result = false;
        if (!cooldownExpires.TryGetValue(key, out var expiration))
            result = true;
        else if (expiration < now)
            result = true;

        if (result)
        {
            var expires = now + Duration.FromSeconds(10);
            cooldownExpires.AddOrUpdate(key, _ => expires, (_, _) => expires);
        }

        return result;
    }

    public TagsApi(GuildDb database)
    {
        this.database = database;
    }

    private static readonly ConcurrentDictionary<TagCooldown, Instant> cooldownExpires = new();
    private readonly GuildDb database;

    private record class TagCooldown(ulong ChannelId, string TagName);
}
