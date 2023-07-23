using Danbo.Models;
using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Danbo.Apis;
using Danbo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Danbo.TypeConverters;

public class TagsAutocomplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ScopedGuildId>()
            .Initialize(context.Guild.Id);

        var tagsApi = scope.ServiceProvider.GetRequiredService<TagsApi>();
        var search = (autocompleteInteraction.Data.Current.Value as string ?? string.Empty)
            .ToUpperInvariant()
            .Split(' ');
        var result = tagsApi.GetTags()
            .Select(x => CalculateRank(search, x))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Select(x => new AutocompleteResult(x.Tag, x.Tag))
            .Take(5);
        return Task.FromResult(AutocompletionResult.FromSuccess(result));
    }

    private static Rank CalculateRank(string[] search, string term)
    {
        int rank = 0;
        foreach (var x in search)
            if (term.ToUpperInvariant().Contains(x)) rank++;

        return new Rank(rank, term);
    }

    private record struct Rank(int Count, string Tag);
}
