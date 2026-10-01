using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;

namespace ThumpersDiscordBot;

internal sealed class Program
{
    private const string AvatarBridgePrefix = "http://127.0.0.1:48125/";
    private const long MaxBridgeRequestBytes = 10 * 1024 * 1024;
    private readonly DiscordSocketClient client = new(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds,
        LogGatewayIntentWarnings = false
    });
    private readonly ConcurrentDictionary<string, BanReview> pendingReviews = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AvatarReview> pendingAvatarReviews = new(StringComparer.Ordinal);
    private BotSettings settings = null!;
    private string bridgeToken = string.Empty;

    private static async Task Main(string[] args)
    {
        var program = new Program();
        var sendTestAlert = args.Any(arg => string.Equals(arg, "--send-test-alert", StringComparison.OrdinalIgnoreCase));
        var sendAvatarTest = args.Any(arg => string.Equals(arg, "--send-avatar-test", StringComparison.OrdinalIgnoreCase));
        await program.RunAsync(sendTestAlert, sendAvatarTest);
    }

    private async Task RunAsync(bool sendTestAlert, bool sendAvatarTest)
    {
        settings = LoadSettings();
        bridgeToken = LocalBridgeSecret.GetOrCreate();
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("DISCORD_BOT_TOKEN is not configured for this Windows user.");

        client.Log += message =>
        {
            Console.WriteLine($"[{message.Severity}] {message.Source}: {message.Message}");
            return Task.CompletedTask;
        };
        TaskCompletionSource? testAlertSent = null;
        if (sendTestAlert)
        {
            testAlertSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Ready += async () =>
            {
                try
                {
                    await SendTestAlertAsync();
                    testAlertSent.SetResult();
                }
                catch (Exception exception)
                {
                    testAlertSent.SetException(exception);
                }
            };
        }
        else
        {
            client.Ready += RegisterCommandsAsync;
            if (sendAvatarTest)
                client.Ready += SendAvatarReviewTestAsync;
        }
        client.SlashCommandExecuted += HandleSlashCommandAsync;
        client.ButtonExecuted += HandleButtonAsync;

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
        if (testAlertSent != null)
        {
            await testAlertSent.Task;
            await client.StopAsync();
            return;
        }

        StartAvatarBridge();
        Console.WriteLine("Thumpers Discord bot is online. Press Ctrl+C to stop.");
        await Task.Delay(Timeout.Infinite);
    }

    private void StartAvatarBridge()
    {
        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(AvatarBridgePrefix);
            listener.Start();
            _ = ListenForAvatarEventsAsync(listener);
            Console.WriteLine("Local avatar bridge is listening on 127.0.0.1:48125.");
        }
        catch (HttpListenerException exception)
        {
            Console.WriteLine($"[Warning] Local avatar bridge is unavailable: {exception.Message}");
        }
    }

    private async Task ListenForAvatarEventsAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = HandleAvatarEventAsync(context);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private async Task HandleAvatarEventAsync(HttpListenerContext context)
    {
        try
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.Ordinal) ||
                !string.Equals(context.Request.Headers["X-AutoMod-Bridge-Token"], bridgeToken, StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            if (context.Request.ContentLength64 is > MaxBridgeRequestBytes)
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                return;
            }

            if (string.Equals(context.Request.Url?.AbsolutePath, "/player-presence", StringComparison.Ordinal))
            {
                await HandlePlayerPresenceAsync(context);
                return;
            }
            if (string.Equals(context.Request.Url?.AbsolutePath, "/logging-stopped", StringComparison.Ordinal))
            {
                await HandleLoggingStoppedAsync(context);
                return;
            }
            if (!string.Equals(context.Request.Url?.AbsolutePath, "/avatar-observed", StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var avatarEvent = await JsonSerializer.DeserializeAsync<AvatarObservedEvent>(context.Request.InputStream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                MaxDepth = 8
            });
            if (avatarEvent == null || string.IsNullOrWhiteSpace(avatarEvent.OwnerUserId) ||
                string.IsNullOrWhiteSpace(avatarEvent.PlayerName) ||
                string.IsNullOrWhiteSpace(avatarEvent.AvatarName))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (!TryReserveAvatarReview(avatarEvent))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            var auditChannel = client.GetChannel(settings.Discord.AuditChannelId) as IMessageChannel;
            if (auditChannel == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                return;
            }

            var avatarId = avatarEvent.AvatarId?.StartsWith("avtr_", StringComparison.OrdinalIgnoreCase) == true
                ? avatarEvent.AvatarId
                : string.Empty;
            var blacklistEntry = string.IsNullOrWhiteSpace(avatarId) ? avatarEvent.AvatarName.Trim() : avatarId;
            var previewImage = DecodePreviewImage(avatarEvent.PreviewImage);
            var previewFileName = GetPreviewFileName(previewImage);
            var review = new AvatarReview(
                Guid.NewGuid().ToString("N"),
                avatarEvent.OwnerUserId,
                avatarEvent.PlayerName,
                avatarEvent.PlayerId ?? string.Empty,
                avatarEvent.AvatarName,
                avatarId,
                blacklistEntry,
                avatarEvent.ThumbnailUrl,
                previewFileName,
                previewImage != null,
                DateTimeOffset.UtcNow.AddMinutes(settings.Moderation.ButtonExpiryMinutes));
            pendingAvatarReviews[review.Id] = review;
            var embed = BuildAvatarEmbed(review, AvatarBlacklistStore.Contains(review.OwnerUserId, review.BlacklistEntry)
                ? "Avatar is currently blacklisted"
                : "Pending staff review", null);
            if (previewImage != null)
            {
                await using var imageStream = new MemoryStream(previewImage);
                await auditChannel.SendFileAsync(imageStream, previewFileName, embed: embed, components: BuildAvatarButtons(review.Id));
            }
            else
            {
                await auditChannel.SendMessageAsync(embed: embed, components: BuildAvatarButtons(review.Id));
            }
            context.Response.StatusCode = (int)HttpStatusCode.Accepted;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Warning] Avatar bridge request failed: {exception.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task HandleLoggingStoppedAsync(HttpListenerContext context)
    {
        if (context.Request.ContentLength64 is > MaxBridgeRequestBytes)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
            return;
        }

        var session = await JsonSerializer.DeserializeAsync<LoggingSessionEvent>(context.Request.InputStream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = 8
        });
        if (session == null || string.IsNullOrWhiteSpace(session.OwnerUserId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        Console.WriteLine("[Info] Logging session stopped; avatar alerts have no de-duplication state.");
        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
    }

    private async Task HandlePlayerPresenceAsync(HttpListenerContext context)
    {
        if (context.Request.ContentLength64 is > MaxBridgeRequestBytes)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
            return;
        }

        var presence = await JsonSerializer.DeserializeAsync<PlayerPresenceEvent>(context.Request.InputStream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = 8
        });
        if (presence == null || string.IsNullOrWhiteSpace(presence.OwnerUserId) ||
            string.IsNullOrWhiteSpace(presence.PlayerName) || string.IsNullOrWhiteSpace(presence.PlayerId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var auditChannel = client.GetChannel(settings.Discord.AuditChannelId) as IMessageChannel;
        if (auditChannel == null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            return;
        }

        var action = presence.Joined ? "joined" : "left";
        var embed = new EmbedBuilder()
            .WithTitle(presence.Joined ? "Player joined" : "Player left")
            .WithColor(presence.Joined ? Color.Green : Color.LightGrey)
            .WithDescription($"**{presence.PlayerName}** {action} the instance.")
            .AddField("Player", presence.PlayerName, true)
            .AddField("User ID", presence.PlayerId, true)
            .AddField("Profile", $"https://vrchat.com/home/user/{presence.PlayerId}", false)
            .AddField("World", presence.WorldName ?? "Unknown world", true)
            .AddField("World ID", presence.WorldId ?? "Unknown", false)
            .AddField("Instance", presence.InstanceId ?? "Unknown", false)
            .WithCurrentTimestamp()
            .Build();
        await auditChannel.SendMessageAsync(embed: embed);
        Console.WriteLine($"[Info] Bot posted player {action} alert for {presence.PlayerName}.");
        context.Response.StatusCode = (int)HttpStatusCode.Accepted;
    }

    private bool TryReserveAvatarReview(AvatarObservedEvent avatarEvent)
    {
        return true;
    }

    private static byte[]? DecodePreviewImage(string? encodedImage)
    {
        if (string.IsNullOrWhiteSpace(encodedImage))
            return null;

        try
        {
            var image = Convert.FromBase64String(encodedImage);
            return image.Length is > 0 and <= 8 * 1024 * 1024 ? image : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string GetPreviewFileName(byte[]? image)
    {
        if (image is { Length: >= 8 } && image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "avatar-preview.png";
        if (image is { Length: >= 3 } && image[0] == 255 && image[1] == 216 && image[2] == 255)
            return "avatar-preview.jpg";
        if (image is { Length: >= 6 } && image.AsSpan(0, 6).SequenceEqual("GIF87a"u8))
            return "avatar-preview.gif";
        if (image is { Length: >= 12 } && image.AsSpan(0, 4).SequenceEqual("RIFF"u8) && image.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "avatar-preview.webp";

        return "avatar-preview.jpg";
    }

    private async Task SendTestAlertAsync()
    {
        var auditChannel = client.GetChannel(settings.Discord.AuditChannelId) as IMessageChannel
            ?? throw new InvalidOperationException("The configured audit channel was not found.");

        var embed = new EmbedBuilder()
            .WithTitle("AutoMod bot test alert")
            .WithDescription("This confirms that the standalone Discord bot can post to the configured audit channel. No VRChat moderation action was taken.")
            .WithColor(Color.Blue)
            .AddField("VRChat group", settings.Vrchat.GroupId, false)
            .WithCurrentTimestamp()
            .Build();
        await auditChannel.SendMessageAsync(embed: embed);
        Console.WriteLine("Test alert sent to the configured audit channel.");
    }

    private async Task SendAvatarReviewTestAsync()
    {
        var auditChannel = client.GetChannel(settings.Discord.AuditChannelId) as IMessageChannel
            ?? throw new InvalidOperationException("The configured audit channel was not found.");
        var cachedImage = TryReadVrcxCacheImage();
        var previewImage = cachedImage ?? Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL6ZwAAAABJRU5ErkJggg==");
        var previewFileName = GetPreviewFileName(previewImage);
        var review = new AvatarReview(
            Guid.NewGuid().ToString("N"),
            "usr_avatar-bridge-test",
            "Avatar Bridge Test",
            "usr_avatar-bridge-test",
            cachedImage != null ? "Local VRCX cache image" : "Built-in test image",
            "avtr_avatar-bridge-test",
            "avtr_avatar-bridge-test",
            null,
            previewFileName,
            true,
            DateTimeOffset.UtcNow.AddMinutes(settings.Moderation.ButtonExpiryMinutes));
        pendingAvatarReviews[review.Id] = review;
        await using var imageStream = new MemoryStream(previewImage);
        await auditChannel.SendFileAsync(
            imageStream,
            review.PreviewFileName,
            embed: BuildAvatarEmbed(review, "Pending staff review (attachment test)", null),
            components: BuildAvatarButtons(review.Id));
        Console.WriteLine("Avatar review attachment test sent to the configured audit channel.");
    }

    private static byte[]? TryReadVrcxCacheImage()
    {
        try
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VRCX", "ImageCache");
            var image = Directory.EnumerateFiles(cachePath, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(path).Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            return image is { Length: > 0 and <= 8 * 1024 * 1024 }
                ? File.ReadAllBytes(image.FullName)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task RegisterCommandsAsync()
    {
        var guild = client.GetGuild(settings.Discord.GuildId)
            ?? throw new InvalidOperationException("Configured Discord server was not found. Check discord.guildId.");

        var commands = new ApplicationCommandProperties[]
        {
            new SlashCommandBuilder()
                .WithName("automod-status")
                .WithDescription("Check the AutoMod bot configuration.")
                .Build(),
            new SlashCommandBuilder()
                .WithName("review-ban")
                .WithDescription("Create a staff ban review for a VRChat user.")
                .AddOption("user_id", ApplicationCommandOptionType.String, "VRChat user ID beginning with usr_", isRequired: true)
                .AddOption("reason", ApplicationCommandOptionType.String, "Reason for staff review", isRequired: true)
                .Build()
        };

        await guild.BulkOverwriteApplicationCommandAsync(commands);
        Console.WriteLine("Guild slash commands registered.");
    }

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (!await IsAuthorizedStaffAsync(command.User))
        {
            await command.RespondAsync("You do not have permission to use AutoMod moderation commands.", ephemeral: true);
            return;
        }

        if (command.CommandName == "automod-status")
        {
            await command.RespondAsync(
                $"AutoMod bot is online. Staff role: <@&{settings.Discord.StaffRoleId}>. " +
                $"VRChat group: `{settings.Vrchat.GroupId}`.",
                ephemeral: true);
            return;
        }

        if (command.CommandName != "review-ban")
            return;

        var userId = command.Data.Options.First(option => option.Name == "user_id").Value?.ToString()?.Trim() ?? string.Empty;
        var reason = command.Data.Options.First(option => option.Name == "reason").Value?.ToString()?.Trim() ?? string.Empty;
        if (!userId.StartsWith("usr_", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(reason))
        {
            await command.RespondAsync("Provide a VRChat user ID beginning with `usr_` and a reason.", ephemeral: true);
            return;
        }

        var review = new BanReview(
            Guid.NewGuid().ToString("N"), userId, reason, command.User.Id, DateTimeOffset.UtcNow.AddMinutes(settings.Moderation.ButtonExpiryMinutes));
        pendingReviews[review.Id] = review;

        var auditChannel = client.GetChannel(settings.Discord.AuditChannelId) as IMessageChannel;
        if (auditChannel == null)
        {
            pendingReviews.TryRemove(review.Id, out _);
            await command.RespondAsync("The configured audit channel was not found.", ephemeral: true);
            return;
        }

        var embed = BuildReviewEmbed(review, "Pending staff confirmation", command.User);
        var components = new ComponentBuilder()
            .WithButton("Confirm ban", $"review:confirm:{review.Id}", ButtonStyle.Danger)
            .WithButton("Cancel", $"review:cancel:{review.Id}", ButtonStyle.Secondary)
            .Build();
        await auditChannel.SendMessageAsync(embed: embed, components: components);
        await command.RespondAsync("Ban review posted to the staff audit channel.", ephemeral: true);
    }

    private async Task HandleButtonAsync(SocketMessageComponent component)
    {
        if (!await IsAuthorizedStaffAsync(component.User))
        {
            await component.RespondAsync("You do not have permission to approve moderation actions.", ephemeral: true);
            return;
        }

        var parts = component.Data.CustomId.Split(':');
        if (parts.Length == 3 && parts[0] == "avatar")
        {
            await HandleAvatarButtonAsync(component, parts);
            return;
        }

        if (parts.Length != 3 || parts[0] != "review" || !pendingReviews.TryGetValue(parts[2], out var review))
        {
            await component.RespondAsync("This review is no longer active.", ephemeral: true);
            return;
        }

        if (DateTimeOffset.UtcNow > review.ExpiresAtUtc)
        {
            pendingReviews.TryRemove(review.Id, out _);
            await component.UpdateAsync(properties =>
            {
                properties.Embed = BuildReviewEmbed(review, "Expired", component.User);
                properties.Components = DisabledReviewButtons(review.Id);
            });
            return;
        }

        pendingReviews.TryRemove(review.Id, out _);
        var status = parts[1] == "confirm"
            ? "Approved - awaiting local VRChat moderation bridge"
            : "Cancelled";

        await component.UpdateAsync(properties =>
        {
            properties.Embed = BuildReviewEmbed(review, status, component.User);
            properties.Components = DisabledReviewButtons(review.Id);
        });
    }

    private async Task HandleAvatarButtonAsync(SocketMessageComponent component, string[] parts)
    {
        if ((parts[1] != "blacklist" && parts[1] != "unblacklist") ||
            !pendingAvatarReviews.TryRemove(parts[2], out var review))
        {
            await component.RespondAsync("This avatar review is no longer active.", ephemeral: true);
            return;
        }

        if (DateTimeOffset.UtcNow > review.ExpiresAtUtc)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Embed = BuildAvatarEmbed(review, "Review expired", component.User);
                properties.Components = DisabledAvatarButtons(review.Id);
            });
            return;
        }

        var changed = parts[1] == "blacklist"
            ? AvatarBlacklistStore.Add(review.OwnerUserId, review.BlacklistEntry)
            : AvatarBlacklistStore.Remove(review.OwnerUserId, review.BlacklistEntry);
        var verb = parts[1] == "blacklist" ? "Blacklisted" : "Unblacklisted";
        var status = changed ? $"{verb} by {component.User.Mention}" : $"Already {verb.ToLowerInvariant()}";
        await component.UpdateAsync(properties =>
        {
            properties.Embed = BuildAvatarEmbed(review, status, component.User);
            properties.Components = DisabledAvatarButtons(review.Id);
        });
    }

    private Task<bool> IsAuthorizedStaffAsync(SocketUser user)
    {
        return Task.FromResult(user is SocketGuildUser guildUser &&
            guildUser.Guild.Id == settings.Discord.GuildId &&
            guildUser.Roles.Any(role => role.Id == settings.Discord.StaffRoleId));
    }

    private Embed BuildReviewEmbed(BanReview review, string status, SocketUser actor)
    {
        return new EmbedBuilder()
            .WithTitle("VRChat ban review")
            .WithColor(status.StartsWith("Pending", StringComparison.Ordinal) ? Color.Orange : Color.DarkGrey)
            .WithDescription(status)
            .AddField("VRChat user", review.UserId, true)
            .AddField("Requested by", actor.Mention, true)
            .AddField("Reason", review.Reason, false)
            .AddField("Profile", $"https://vrchat.com/home/user/{review.UserId}", false)
            .AddField("Expires", review.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), false)
            .WithCurrentTimestamp()
            .Build();
    }

    private static MessageComponent DisabledReviewButtons(string reviewId)
    {
        return new ComponentBuilder()
            .WithButton("Confirm ban", $"review:confirm:{reviewId}", ButtonStyle.Danger, disabled: true)
            .WithButton("Cancel", $"review:cancel:{reviewId}", ButtonStyle.Secondary, disabled: true)
            .Build();
    }

    private static MessageComponent BuildAvatarButtons(string reviewId)
    {
        return new ComponentBuilder()
            .WithButton("Blacklist avatar", $"avatar:blacklist:{reviewId}", ButtonStyle.Danger)
            .WithButton("Unblacklist avatar", $"avatar:unblacklist:{reviewId}", ButtonStyle.Secondary)
            .Build();
    }

    private static MessageComponent DisabledAvatarButtons(string reviewId)
    {
        return new ComponentBuilder()
            .WithButton("Blacklist avatar", $"avatar:blacklist:{reviewId}", ButtonStyle.Danger, disabled: true)
            .WithButton("Unblacklist avatar", $"avatar:unblacklist:{reviewId}", ButtonStyle.Secondary, disabled: true)
            .Build();
    }

    private static Embed BuildAvatarEmbed(AvatarReview review, string status, SocketUser? actor)
    {
        var builder = new EmbedBuilder()
            .WithTitle($"Player avatar detected: {review.PlayerName}")
            .WithColor(status.StartsWith("Pending", StringComparison.Ordinal) ? Color.Orange : Color.DarkGrey)
            .WithDescription(status)
            .AddField("Player", string.IsNullOrWhiteSpace(review.PlayerId)
                ? review.PlayerName
                : $"{review.PlayerName} (`{review.PlayerId}`)", false)
            .AddField("Avatar", review.AvatarName, true)
            .AddField("Avatar ID", string.IsNullOrWhiteSpace(review.AvatarId) ? "Not exposed by VRChat logs" : review.AvatarId, true)
            .AddField("Profile", string.IsNullOrWhiteSpace(review.PlayerId)
                ? "Unavailable"
                : $"https://vrchat.com/home/user/{review.PlayerId}", false)
            .AddField("Expires", review.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), false)
            .WithCurrentTimestamp();

        if (review.HasPreviewAttachment)
            builder.WithImageUrl($"attachment://{review.PreviewFileName}");
        else if (Uri.TryCreate(review.ThumbnailUrl, UriKind.Absolute, out var thumbnailUri) && thumbnailUri.Scheme == Uri.UriSchemeHttps)
            builder.WithImageUrl(thumbnailUri.ToString());

        return builder.Build();
    }

    private static BotSettings LoadSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "botsettings.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("botsettings.json was not found next to the bot executable.", path);

        var settings = JsonSerializer.Deserialize<BotSettings>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        }) ?? throw new InvalidOperationException("botsettings.json is empty or invalid.");
        settings.Validate();
        return settings;
    }

    private sealed record BanReview(string Id, string UserId, string Reason, ulong RequestedByUserId, DateTimeOffset ExpiresAtUtc);
    private sealed record AvatarReview(string Id, string OwnerUserId, string PlayerName, string PlayerId, string AvatarName, string AvatarId, string BlacklistEntry, string? ThumbnailUrl, string PreviewFileName, bool HasPreviewAttachment, DateTimeOffset ExpiresAtUtc);
    private sealed record AvatarObservedEvent(string OwnerUserId, string PlayerName, string? PlayerId, string? AvatarName, string? AvatarId, string? ThumbnailUrl, string? PreviewImage);
    private sealed record PlayerPresenceEvent(string OwnerUserId, string PlayerName, string PlayerId, bool Joined, string? WorldName, string? WorldId, string? InstanceId);
    private sealed record LoggingSessionEvent(string OwnerUserId);
}

internal static class LocalBridgeSecret
{
    private static readonly string secretPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VRChatInstanceLogger", "discord-bot-bridge.secret");

    public static string GetOrCreate()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(secretPath)!);
        if (File.Exists(secretPath))
            return Unprotect(File.ReadAllText(secretPath).Trim());

        var secret = RandomNumberGenerator.GetBytes(32);
        var protectedSecret = ProtectedData.Protect(secret, null, DataProtectionScope.CurrentUser);
        File.WriteAllText(secretPath, Convert.ToBase64String(protectedSecret));
        return Convert.ToBase64String(secret);
    }

    private static string Unprotect(string value)
    {
        var protectedSecret = Convert.FromBase64String(value);
        var secret = ProtectedData.Unprotect(protectedSecret, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(secret);
    }
}

internal static class AvatarBlacklistStore
{
    private static readonly string storeFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VRChatInstanceLogger", "blacklists");

    public static bool Contains(string ownerUserId, string avatarId)
        => Load(ownerUserId).Contains(avatarId, StringComparer.OrdinalIgnoreCase);

    public static bool Add(string ownerUserId, string avatarId)
    {
        var entries = Load(ownerUserId);
        if (entries.Contains(avatarId, StringComparer.OrdinalIgnoreCase))
            return false;

        entries.Add(avatarId);
        Save(ownerUserId, entries);
        return true;
    }

    public static bool Remove(string ownerUserId, string avatarId)
    {
        var entries = Load(ownerUserId);
        var removed = entries.RemoveAll(entry => string.Equals(entry, avatarId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
            Save(ownerUserId, entries);
        return removed;
    }

    private static List<string> Load(string ownerUserId)
    {
        var path = GetPath(ownerUserId);
        return File.Exists(path)
            ? File.ReadAllLines(path).Select(value => value.Trim()).Where(value => !string.IsNullOrWhiteSpace(value) && !value.StartsWith("#")).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : [];
    }

    private static void Save(string ownerUserId, IEnumerable<string> entries)
    {
        Directory.CreateDirectory(storeFolder);
        File.WriteAllLines(GetPath(ownerUserId), entries);
    }

    private static string GetPath(string ownerUserId)
    {
        var safeUserId = new string(ownerUserId.Where(character => char.IsLetterOrDigit(character) || character is '_' or '-').ToArray());
        return Path.Combine(storeFolder, (string.IsNullOrWhiteSpace(safeUserId) ? "unknown" : safeUserId) + ".avatars.txt");
    }
}

internal sealed class BotSettings
{
    public DiscordSettings Discord { get; init; } = new();
    public VrchatSettings Vrchat { get; init; } = new();
    public ModerationSettings Moderation { get; init; } = new();

    public void Validate()
    {
        if (Discord.GuildId == 0 || Discord.StaffRoleId == 0 || Discord.AuditChannelId == 0 ||
            string.IsNullOrWhiteSpace(Vrchat.GroupId) || Moderation.ButtonExpiryMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("botsettings.json is missing a required Discord ID, VRChat group ID, or valid button expiry.");
        }
    }
}

internal sealed class DiscordSettings
{
    public ulong GuildId { get; init; }
    public ulong StaffRoleId { get; init; }
    public ulong AuditChannelId { get; init; }
}

internal sealed class VrchatSettings
{
    public string GroupId { get; init; } = string.Empty;
}

internal sealed class ModerationSettings
{
    public bool RequireBanConfirmation { get; init; } = true;
    public int ButtonExpiryMinutes { get; init; } = 10;
}