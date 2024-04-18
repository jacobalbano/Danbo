using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models.Jobs;

public record class ServerLogJob(ulong GuildId);

public record class MessageChangeLogJob(ITextChannel Channel, IMessage Message, string CachedContent = "")
    : ServerLogJob(Channel.GuildId);

public record class MessageUpdateLogJob(ITextChannel Channel, IMessage Message, string CachedContent = "")
    : MessageChangeLogJob(Channel, Message, CachedContent);

public record class MessageDeleteLogJob(ITextChannel Channel, IMessage Message)
    : MessageChangeLogJob(Channel, Message, Message?.Content);

public record class UserLeftLogJob(ulong GuildId, IUser User)
    : ServerLogJob(GuildId);