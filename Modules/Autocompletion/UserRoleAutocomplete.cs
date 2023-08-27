using Discord;
using Discord.Interactions;
using Danbo.Services;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Danbo.Models;
using Danbo.Apis;
using Microsoft.Extensions.DependencyInjection;

namespace Danbo.Modules.Autocompletion;

public class UserRoleAutocomplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var db = services.GetRequiredService<Database>();
        var search = (autocompleteInteraction.Data.Current.Value as string ?? string.Empty)
            .ToUpperInvariant()
            .Split(' ');
        var result = db.Select<UserRole>()
            .ToEnumerable()
            .Select(x => context.Guild.GetRole(x.Id))
            .Select(x => CalculateRank(search, x))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Select(x => new AutocompleteResult(x.Role.Name, x.Role.Id.ToString()))
            .Take(25);
        return Task.FromResult(AutocompletionResult.FromSuccess(result));
    }

    private static Rank CalculateRank(string[] search, IRole role)
    {
        int rank = 0;
        foreach (var x in search)
            if (role.Name.ToUpperInvariant().Contains(x)) rank++;

        return new Rank(rank, role);
    }

    private record struct Rank(int Count, IRole Role);
}