using Danbo.Models;
using Danbo.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class TagsApi
{
    public void SetTag(string tagName, string? content)
    {
        var tag = database
            .Select<Tag>()
            .FirstOrDefault(x => x.Name == tagName)
            ?? new Tag { Name = tagName };

        using var s = database.BeginSession();
        if (content == null) s.Delete(tag);
        else s.InsertOrUpdate(tag with { Text = content });
    }

    public string? GetTag(string tagName)
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
            .ToList();
    }

    public TagsApi(Database database)
    {
        this.database = database;
    }

    private readonly Database database;
}
