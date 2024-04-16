using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models.Jobs;

public enum MessageChangeType { None, Updated, Deleted }
public record class ServerLogJob(ITextChannel Channel, MessageChangeType Type, IMessage Message, string CachedContent = "");