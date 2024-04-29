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

public class InfractionAutocomplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        ulong filterUserId;
        try
        {
            filterUserId = autocompleteInteraction
                .Data.Options
                .Where(x => x.Type == ApplicationCommandOptionType.User)
                .Select(x => ulong.TryParse((string)x.Value, out var id) ? id : (ulong?)null)
                .FirstOrDefault() ?? throw new Exception("Please supply a value to the User parameter");
        }
        catch (Exception ex)
        {
            return Task.FromResult(AutocompletionResult.FromError(ex));
        }

        var db = services.GetRequiredService<GuildDb>();
        var search = autocompleteInteraction.Data.Current.Value as string ?? string.Empty;
        var words = search.Split(' ');
        var result = db.Select<Infraction>()
            .Where(x => x.UserId == filterUserId)
            .Where(x => x.Message.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToEnumerable()
            .Select(x => CalculateRank(words, x))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Select(x => new AutocompleteResult($"{x.Infraction.Message.Truncate(32)}", x.Infraction.Key.ToString()))
            .Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(result));
    }

    private static Rank CalculateRank(string[] search, Infraction infraction)
    {
        int rank = 0;
        foreach (var x in search)
            if (infraction.Message.Contains(x, StringComparison.InvariantCultureIgnoreCase)) rank++;

        return new Rank(rank, infraction);
    }

    private record struct Rank(int Count, Infraction Infraction);
}