using Danbo.Services;
using Danbo.Transients.HelpProviders;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Modules;

[RequireContext(ContextType.Guild)]
public class HelpModule : ModuleBase
{
    [SlashCommand("help", "Show bot features", ignoreGroupNames: true)]
    public async Task GetHelp()
    {
        var defer = DeferAsync();
        var builder = await MakeHelpBuilder(Context, providers);

        await defer;
        await FollowupAsync(embed: builder.Build());
    }

    [SlashCommand("public-help", "Run the /help command without displaying privileged commands")]
    [RequireUserPermission(GuildPermission.ModerateMembers), DefaultMemberPermissions(GuildPermission.ModerateMembers)]
    public async Task ShowPublicHelp()
    {
        await RespondAsync("Displaying menu", ephemeral: true);
        var builder = await MakeHelpBuilder(new UnprivilegedContext(Context), providers);
        await Context.Channel.SendMessageAsync(embed: builder.Build());
    }

    private static async Task<EmbedBuilder> MakeHelpBuilder(IInteractionContext context, IReadOnlyList<IHelpProvider> providers)
    {
        var builder = new EmbedBuilder();
        var sb = new StringBuilder();

        foreach (var p in providers)
        {
            sb.AppendLine($"## {p.FeatureName}");
            await foreach (var str in p.FeaturesAvailable(context))
                sb.AppendLine($"- {str}");
        }

        return builder.WithDescription(sb.ToString());
    }

    public HelpModule(IEnumerable<IHelpProvider> providers)
    {
        this.providers = providers
            .OrderBy(x => x.FeatureName)
            .ToList();
    }

    private readonly IReadOnlyList<IHelpProvider> providers;

    private class UnprivilegedContext : IInteractionContext
    {
        public IDiscordClient Client { get; }

        public IGuild Guild { get; }

        public IMessageChannel Channel { get; }

        public IUser User { get; }

        public IDiscordInteraction Interaction { get; }

        public UnprivilegedContext(IInteractionContext context)
        {
            var user = context.User as IGuildUser;
            var uuser = new UnprivilegedUser(user);
            Interaction = new UnprivelegedInteraction(context.Interaction, uuser);
            User = uuser;

            Client = context.Client;
            Guild = context.Guild;
            Channel = context.Channel;
        }

        private class UnprivilegedUser : IGuildUser
        {
            public UnprivilegedUser(IGuildUser user)
            {
                this.user = user;
            }

            private readonly IGuildUser user;
            public DateTimeOffset? JoinedAt => user.JoinedAt;
            public string DisplayName => user.DisplayName;
            public string Nickname => user.Nickname;
            public string DisplayAvatarId => user.DisplayAvatarId;
            public string GuildAvatarId => user.GuildAvatarId;
            public GuildPermissions GuildPermissions => GuildPermissions.None;
            public IGuild Guild => user.Guild;
            public ulong GuildId => user.GuildId;
            public DateTimeOffset? PremiumSince => user.PremiumSince;
            public IReadOnlyCollection<ulong> RoleIds => user.RoleIds;
            public bool? IsPending => user.IsPending;
            public int Hierarchy => user.Hierarchy;
            public DateTimeOffset? TimedOutUntil => user.TimedOutUntil;
            public GuildUserFlags Flags => user.Flags;
            public string AvatarId => user.AvatarId;
            public string Discriminator => user.Discriminator;
            public ushort DiscriminatorValue => user.DiscriminatorValue;
            public bool IsBot => user.IsBot;
            public bool IsWebhook => user.IsWebhook;
            public string Username => user.Username;
            public UserProperties? PublicFlags => user.PublicFlags;
            public string GlobalName => user.GlobalName;
            public DateTimeOffset CreatedAt => user.CreatedAt;
            public ulong Id => 0;
            public string Mention => user.Mention;
            public UserStatus Status => user.Status;
            public IReadOnlyCollection<ClientType> ActiveClients => user.ActiveClients;
            public IReadOnlyCollection<IActivity> Activities => user.Activities;
            public bool IsDeafened => user.IsDeafened;
            public bool IsMuted => user.IsMuted;
            public bool IsSelfDeafened => user.IsSelfDeafened;
            public bool IsSelfMuted => user.IsSelfMuted;
            public bool IsSuppressed => user.IsSuppressed;
            public IVoiceChannel VoiceChannel => user.VoiceChannel;
            public string VoiceSessionId => user.VoiceSessionId;
            public bool IsStreaming => user.IsStreaming;
            public bool IsVideoing => user.IsVideoing;
            public DateTimeOffset? RequestToSpeakTimestamp => user.RequestToSpeakTimestamp;
            public ChannelPermissions GetPermissions(IGuildChannel channel) => ChannelPermissions.None;
            public string GetGuildAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128) => user.GetGuildAvatarUrl(format, size);
            public string GetDisplayAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128) => user.GetDisplayAvatarUrl(format, size);
            public Task KickAsync(string reason = null, RequestOptions options = null) => user.KickAsync(reason, options);
            public Task ModifyAsync(Action<GuildUserProperties> func, RequestOptions options = null) => user.ModifyAsync(func, options);
            public Task AddRoleAsync(ulong roleId, RequestOptions options = null) => user.AddRoleAsync(roleId, options);
            public Task AddRoleAsync(IRole role, RequestOptions options = null) => user.AddRoleAsync(role, options);
            public Task AddRolesAsync(IEnumerable<ulong> roleIds, RequestOptions options = null) => user.AddRolesAsync(roleIds, options);
            public Task AddRolesAsync(IEnumerable<IRole> roles, RequestOptions options = null) => user.AddRolesAsync(roles, options);
            public Task RemoveRoleAsync(ulong roleId, RequestOptions options = null) => user.RemoveRoleAsync(roleId, options);
            public Task RemoveRoleAsync(IRole role, RequestOptions options = null) => user.RemoveRoleAsync(role, options);
            public Task RemoveRolesAsync(IEnumerable<ulong> roleIds, RequestOptions options = null) => user.RemoveRolesAsync(roleIds, options);
            public Task RemoveRolesAsync(IEnumerable<IRole> roles, RequestOptions options = null) => user.RemoveRolesAsync(roles, options);
            public Task SetTimeOutAsync(TimeSpan span, RequestOptions options = null) => user.SetTimeOutAsync(span, options);
            public Task RemoveTimeOutAsync(RequestOptions options = null) => user.RemoveTimeOutAsync(options);
            public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128) => user.GetAvatarUrl(format, size);
            public string GetDefaultAvatarUrl() => user.GetDefaultAvatarUrl();
            public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null) => user.CreateDMChannelAsync(options);
        }

        private class UnprivelegedInteraction : IDiscordInteraction
        {
            public UnprivelegedInteraction(IDiscordInteraction interaction, UnprivilegedUser user)
            {
                User = user;
                this.interaction = interaction;
            }

            public ulong Id => interaction.Id;
            public InteractionType Type => interaction.Type;
            public IDiscordInteractionData Data => interaction.Data;
            public string Token => interaction.Token;
            public int Version => interaction.Version;
            public bool HasResponded => interaction.HasResponded;
            public IUser User { get; }
            public string UserLocale => interaction.UserLocale;
            public string GuildLocale => interaction.GuildLocale;
            public bool IsDMInteraction => interaction.IsDMInteraction;
            public ulong? ChannelId => interaction.ChannelId;
            public ulong? GuildId => interaction.GuildId;
            public ulong ApplicationId => interaction.ApplicationId;
            public DateTimeOffset CreatedAt => interaction.CreatedAt;
            public Task DeferAsync(bool ephemeral = false, RequestOptions options = null) => interaction.DeferAsync(ephemeral, options);
            public Task DeleteOriginalResponseAsync(RequestOptions options = null) => interaction.DeleteOriginalResponseAsync(options);
            public Task<IUserMessage> FollowupAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, MessageComponent components = null, Embed embed = null, RequestOptions options = null) => interaction.FollowupAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options);
            public Task<IUserMessage> FollowupWithFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, MessageComponent components = null, Embed embed = null, RequestOptions options = null) => interaction.FollowupWithFilesAsync(attachments, text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options);
            public Task<IUserMessage> GetOriginalResponseAsync(RequestOptions options = null) => interaction.GetOriginalResponseAsync(options);
            public Task<IUserMessage> ModifyOriginalResponseAsync(Action<MessageProperties> func, RequestOptions options = null) => interaction.ModifyOriginalResponseAsync(func, options);
            public Task RespondAsync(string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, MessageComponent components = null, Embed embed = null, RequestOptions options = null) => interaction.RespondAsync(text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options);
            public Task RespondWithFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, Embed[] embeds = null, bool isTTS = false, bool ephemeral = false, AllowedMentions allowedMentions = null, MessageComponent components = null, Embed embed = null, RequestOptions options = null) => interaction.RespondWithFilesAsync(attachments, text, embeds, isTTS, ephemeral, allowedMentions, components, embed, options);
            public Task RespondWithModalAsync(Modal modal, RequestOptions options = null) => interaction.RespondWithModalAsync(modal, options);
            private IDiscordInteraction interaction;
        }
    }
}
