using Danbo.Apis;
using Discord.Interactions;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;
using Danbo.Services;
using Danbo.Models.Jobs;
using NodaTime.Text;

namespace Danbo.Modules.Autocompletion;

internal class AnalysisJobAutocomplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        using var scope = services.GuildScope(context.Guild.Id);
        var tz = scope.ServiceProvider.GetRequiredService<TimezoneProviderService>().Tzdb.GetSystemDefault();

        var analysisApi = scope.ServiceProvider.GetRequiredService<AnalysisApi>();
        var search = (autocompleteInteraction.Data.Current.Value as string ?? string.Empty)
            .ToUpperInvariant()
            .Split(' ');
        var result = analysisApi.GetJobs()
            .Where(x => x.State <= AnalysisState.Paused)
            .Select(x => CalculateRank(search, x, tz))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Select(x => new AutocompleteResult(x.Tag, x.KeyGuid))
            .Take(25);
        return Task.FromResult(AutocompletionResult.FromSuccess(result));
    }

    private static Rank CalculateRank(string[] search, AnalysisJob job, DateTimeZone tz)
    {
        var term = "Job started " + job.FirstStarted.InZone(tz)
            .ToString("uuuu-MM-dd H:mm", null);

        int rank = search.Count(x => term.Contains(x));
        return new Rank(rank, term, job.Key.ToString());
    }

    private record struct Rank(int Count, string Tag, string KeyGuid);
}
