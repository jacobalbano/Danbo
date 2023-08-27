using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.TypeConverters;

public class GuildPermissionsTypeConverter : TypeConverter<GuildPermissions>
{
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.Mentionable;

    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        return option.Value switch
        {
            IGuildUser guildUser => Task.FromResult(TypeConverterResult.FromSuccess(guildUser.GuildPermissions)),
            IRole role => Task.FromResult(TypeConverterResult.FromSuccess(role.Permissions)),
            _ => Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, option.Value.ToString() + " is not a Guild Role or Guild User")),
        };
    }

    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Description = "This description is added dynamically.";
    }
}