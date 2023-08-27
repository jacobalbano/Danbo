using Danbo.Models;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules.Autocompletion;

public class ConfigClassAutocomplete<TConfig> : AutocompleteHandler
    where TConfig : ModelBase
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        return Task.FromResult(AutocompletionResult.FromSuccess(names));
    }

    private static readonly IReadOnlyList<AutocompleteResult> names = typeof(TConfig)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Select(x => new AutocompleteResult(x.Name, x.Name))
        .Take(25)
        .ToList();
}
