using Danbo.Apis;
using Danbo.Errors;
using Danbo.Modules.Parameters;
using Danbo.Services;
using Danbo.Utility;
using Discord;
using Discord.Interactions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Danbo.Modules;

[RequireContext(ContextType.Guild)]
public class OntopicModule : ModuleBase
{
    [SlashCommand("ontopic", "Block access to offtopic channels for a given amount of time")]
    public async Task GoOntopic(int amount, DurationUnit unit)
    {
        try
        {
            var defer = DeferAsync();
            var duration = unit.ToDuration(amount);

            if (amount <= 0)
            {
                await defer;
                await FollowupAsync(embed: EmbedUtility.Error("Invalid value."));
                return;
            }

            if (duration > maxDuration)
            {
                await defer;
                await FollowupAsync(embed: EmbedUtility.Error(@"
                Please limit your use of this command to intervals of 30 days or less.
                For longer terms, contact a moderator directly."
                ));
                return;
            }

            var expiration = SystemClock.Instance.GetCurrentInstant() + duration;
            await ontopic.AddOntopicToUser((IGuildUser)Context.User, expiration);

            var embed = EmbedUtility.Message(
                $"Ontopic applied until <t:{expiration.ToUnixTimeSeconds()}:f>"
            );

            await FollowupAsync(embed: embed);
            try { await Context.User.SendMessageAsync(embed: embed); }
            catch (Exception) { }
        }
        catch (UserFacingError e)
        {
            await FollowupAsync(embed: EmbedUtility.Error(e));
            return;
        }
    }

    public OntopicModule(OntopicApi ontopic)
    {
        this.ontopic = ontopic;
    }

    private static readonly Duration maxDuration = Duration.FromDays(30);
    private readonly OntopicApi ontopic;
}
