using Danbo.Errors;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Utility;

public static class EmbedUtility
{
    public static Embed Error(string message) => new EmbedBuilder()
        .WithColor(Color.Red)
        .WithDescription(message)
        .Build();

    public static Embed Message(string message) => new EmbedBuilder()
        .WithDescription(message)
        .Build();

    public static Embed Error(UserFacingError e) => Error(e.Message);
}
