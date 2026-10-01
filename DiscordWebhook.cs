using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VRChatInstanceLogger
{
    /// <summary>
    /// Posts moderation alerts to a Discord channel via an incoming webhook.
    ///
    /// Webhooks are bound to the VRChat account that is logged in, so each person who
    /// uses the app sends alerts to their own Discord staff-log channel. A logged-in
    /// account's webhook is remembered (encrypted per Windows user) in webhooks.dat.
    ///
    /// To enroll a webhook, paste it into webhook_config.txt in the app-data folder:
    ///   url=https://discord.com/api/webhooks/XXXX/YYYY
    /// The next time that account logs in, the URL is adopted and bound to it.
    ///
    /// Sends are serialized (one at a time) and respect Discord's 429 rate limit so a
    /// busy instance cannot burst dozens of requests and get the webhook throttled.
    /// </summary>
    public class DiscordWebhook
    {
        private const string ConfigFileName = "webhook_config.txt";
        private const string StoreFileName = "webhooks.dat";

        // Reusable colors (decimal RGB) for embed accents.
        public const int ColorBan = 0xED4245;         // red   - a ban happened
        public const int ColorGroupMatch = 0xE67E22;  // orange- blacklisted group matched
        public const int ColorAvatarMatch = 0xF1C40F; // gold  - blacklisted avatar matched
        public const int ColorRestricted = 0xFAA61A;  // amber - restricted-area detection
        public const int ColorRip = 0x9B59B6;         // purple- rip/asset-theft suspicion
        public const int ColorJoin = 0x2ECC71;        // green - player joined (brand color)
        public const int ColorLeave = 0x95A5A6;       // gray  - player left
        public const int ColorInfo = 0x2ECC71;        // green - general info (brand color)
        public const int ColorStaffAction = 0x5865F2; // blurple - manual staff moderation

        private static readonly HttpClient http = CreateClient();
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);

        private static readonly string StoreFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatInstanceLogger");
        private static readonly string StorePath = Path.Combine(StoreFolder, StoreFileName);

        private readonly string configPath;

        // Watches webhook_config.txt so a URL change is picked up live (while logged in)
        // without requiring a log out / log in.
        private FileSystemWatcher? configWatcher;
        private readonly object reloadLock = new object();
        private DateTime lastConfigReloadUtc = DateTime.MinValue;

        // URL pasted into webhook_config.txt, used to enroll the next account that logs in.
        private string configUrl = string.Empty;
        // Optional forum-channel webhook: ban/moderation actions are posted here as new
        // forum posts. Enrolled per account just like the main webhook.
        private string configForumUrl = string.Empty;
        // The account (VRChat user id) whose webhook is currently active, and its URLs.
        private string activeUserId = string.Empty;
        private string activeUrl = string.Empty;
        private string activeForumUrl = string.Empty;

        public event Action<string>? OnLog;

        public DiscordWebhook()
        {
            configPath = RuntimePaths.GetFile(ConfigFileName);
        }

        // Alerts only send while an account with a bound webhook is logged in.
        public bool IsEnabled => !string.IsNullOrWhiteSpace(activeUrl);

        // Forum posts only send while an account with a bound forum webhook is logged in.
        public bool IsForumEnabled => !string.IsNullOrWhiteSpace(activeForumUrl);

        public string ActiveUrl => activeUrl;

        public string ActiveForumUrl => activeForumUrl;

        /// <summary>
        /// Binds the webhook to the account that just logged in. Loads that account's saved
        /// webhook; if it has none but webhook_config.txt holds a valid URL, that URL is
        /// adopted and remembered for the account. Returns the resulting status so the caller
        /// can tell the user whether alerts are active or how to enable them.
        /// </summary>
        public WebhookBindResult SetActiveUser(string? userId)
        {
            activeUserId = userId?.Trim() ?? string.Empty;
            activeUrl = string.Empty;
            activeForumUrl = string.Empty;

            if (string.IsNullOrWhiteSpace(activeUserId))
                return WebhookBindResult.NotConfigured;

            var store = LoadStore();
            var storeChanged = false;
            var result = WebhookBindResult.NotConfigured;

            // Main alert webhook.
            var hasSaved = store.TryGetValue(activeUserId, out var saved) && IsLikelyWebhookUrl(saved);
            if (hasSaved)
            {
                activeUrl = saved!;
                result = WebhookBindResult.Existing;
            }

            var forumKey = ForumKey(activeUserId);
            var hasSavedForum = store.TryGetValue(forumKey, out var savedForum) && IsLikelyWebhookUrl(savedForum);
            if (hasSavedForum)
            {
                activeForumUrl = savedForum!;
            }

            if (storeChanged)
                SaveStore(store);

            return result;
        }

        public bool UpdateActiveUrls(string? alertUrl, string? forumUrl, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(activeUserId))
            {
                error = "Log in before saving webhook links.";
                return false;
            }

            var normalizedAlert = alertUrl?.Trim() ?? string.Empty;
            var normalizedForum = forumUrl?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedAlert) && !IsLikelyWebhookUrl(normalizedAlert))
            {
                error = "The top link is not a valid Discord webhook URL.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(normalizedForum) && !IsLikelyWebhookUrl(normalizedForum))
            {
                error = "The bottom link is not a valid Discord webhook URL.";
                return false;
            }

            var store = LoadStore();
            SetStoreValue(store, activeUserId, normalizedAlert);
            SetStoreValue(store, ForumKey(activeUserId), normalizedForum);
            SaveStore(store);
            activeUrl = normalizedAlert;
            activeForumUrl = normalizedForum;
            return true;
        }

        private static void SetStoreValue(Dictionary<string, string> store, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                store.Remove(key);
            else
                store[key] = value;
        }

        // Clears the active webhook binding (call on logout).
        public void ClearActiveUser()
        {
            activeUserId = string.Empty;
            activeUrl = string.Empty;
            activeForumUrl = string.Empty;
        }

        // Per-account storage key for the optional forum webhook URL.
        private static string ForumKey(string userId) => userId + "#forum";

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VRChat-Group-Auto-Moderation");
            return client;
        }

        // Re-reads webhook_config.txt from disk and re-applies it to the active account.
        // Useful if the user edits the file while the app is open. Returns the binding status.
        public WebhookBindResult Reload()
        {
            LoadConfig();
            return SetActiveUser(activeUserId);
        }

        // Begins watching webhook_config.txt for edits so a new/changed URL is applied live
        // (while an account is logged in) without needing a log out / log in. Best-effort:
        // if the watcher cannot be created, manual re-login still adopts config changes.
        private void StartConfigWatcher()
        {
            try
            {
                var dir = Path.GetDirectoryName(configPath);
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    return;

                configWatcher = new FileSystemWatcher(dir, ConfigFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                configWatcher.Changed += OnConfigFileChanged;
                configWatcher.Created += OnConfigFileChanged;
                configWatcher.Renamed += OnConfigFileChanged;
            }
            catch
            {
                // File watching is optional; the config is still adopted on next login.
            }
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            // Editors often fire several events per save; debounce so we reload once.
            lock (reloadLock)
            {
                var now = DateTime.UtcNow;
                if ((now - lastConfigReloadUtc).TotalMilliseconds < 750)
                    return;
                lastConfigReloadUtc = now;
            }

            // The URL is bound per account, so only meaningful while someone is logged in.
            if (string.IsNullOrWhiteSpace(activeUserId))
                return;

            try
            {
                // Give the writing program a moment to finish flushing the file to disk.
                System.Threading.Thread.Sleep(200);
                ReloadFromConfigLive();
            }
            catch
            {
                // Never let a file-system event crash the app.
            }
        }

        // Re-reads webhook_config.txt and applies any changed URL to the logged-in account
        // without a re-login. Only logs when a bound URL actually changes, to avoid noise.
        private void ReloadFromConfigLive()
        {
            if (string.IsNullOrWhiteSpace(activeUserId))
                return;

            var previousUrl = activeUrl;
            var previousForumUrl = activeForumUrl;

            LoadConfig();
            SetActiveUser(activeUserId);

            if (!string.Equals(previousUrl, activeUrl, StringComparison.Ordinal) && IsEnabled)
                OnLog?.Invoke("[WEBHOOK] Alert webhook URL updated from webhook_config.txt (no re-login needed).");

            if (!string.Equals(previousForumUrl, activeForumUrl, StringComparison.Ordinal) && IsForumEnabled)
                OnLog?.Invoke("[WEBHOOK] Forum webhook URL updated from webhook_config.txt (no re-login needed).");
        }

        private void LoadConfig()
        {
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath,
@"# Discord webhook settings (per person).
#
# Each person who logs in sends alerts to THEIR OWN Discord staff-log channel.
# Paste your channel's webhook URL below, then log in: it will be bound to your
# account and remembered, so you only have to do this once.
#
# In Discord: Server Settings > Integrations > Webhooks > New Webhook > Copy Webhook URL.
#
# ---- EXAMPLE ONLY (do NOT put your URL on these lines) ----
# Example:
#   url=https://discord.com/api/webhooks/123456789012345678/abcDEF...
# -----------------------------------------------------------
#
# >>> Put your real webhook URL on the line below, right after url= <<<
url=

# ---------------------------------------------------------------------------
# OPTIONAL - FORUM AUTO-MOD LOG
# ---------------------------------------------------------------------------
# If you also want every BAN / MODERATION action posted as its own post in a
# Discord FORUM channel, create a webhook on that forum channel and paste it
# below. Each banned player becomes a new forum post titled with their name.
# Only ban/moderation actions are posted here - NOT joins, leaves, or info.
# Leave blank to disable.
forum_url=
");
                configUrl = string.Empty;
                configForumUrl = string.Empty;
                return;
            }

            var parsedUrl = string.Empty;
            var parsedForumUrl = string.Empty;
            foreach (var raw in File.ReadAllLines(configPath))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                var key = line.Substring(0, idx).Trim().ToLowerInvariant();
                var value = line.Substring(idx + 1).Trim();

                if (key == "url")
                    parsedUrl = value;
                else if (key == "forum_url")
                    parsedForumUrl = value;
            }

            configUrl = IsLikelyWebhookUrl(parsedUrl) ? parsedUrl : string.Empty;
            configForumUrl = IsLikelyWebhookUrl(parsedForumUrl) ? parsedForumUrl : string.Empty;

            if (!string.IsNullOrWhiteSpace(parsedUrl) && string.IsNullOrWhiteSpace(configUrl))
                OnLog?.Invoke("[WEBHOOK] The url in webhook_config.txt does not look like a Discord webhook URL.");

            if (!string.IsNullOrWhiteSpace(parsedForumUrl) && string.IsNullOrWhiteSpace(configForumUrl))
                OnLog?.Invoke("[WEBHOOK] The forum_url in webhook_config.txt does not look like a Discord webhook URL.");
        }

        // Loads the per-account webhook map (userId -> url), decrypted for the current Windows user.
        private static Dictionary<string, string> LoadStore()
        {
            try
            {
                if (!File.Exists(StorePath))
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var encrypted = File.ReadAllBytes(StorePath);
                var jsonBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonBytes);
                return map == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // Persists the per-account webhook map, encrypted per Windows user.
        private static void SaveStore(Dictionary<string, string> store)
        {
            try
            {
                if (!Directory.Exists(StoreFolder))
                    Directory.CreateDirectory(StoreFolder);

                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(store);
                var encrypted = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(StorePath, encrypted);
            }
            catch
            {
                // Ignore persistence errors; the webhook simply won't be remembered.
            }
        }

        private static bool IsLikelyWebhookUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttps
                && (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase))
                && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.Length > "/api/webhooks/".Length;
        }

        /// <summary>
        /// Sends a single rich embed. Fire-and-forget friendly: callers can ignore the
        /// returned task. Failures are reported through <see cref="OnLog"/> and never throw.
        /// </summary>
        public async Task SendEmbedAsync(
            string title,
            string? description,
            int color,
            IEnumerable<(string Name, string Value, bool Inline)>? fields = null,
            string? imageUrl = null)
        {
            if (!IsEnabled)
                return;

            JObject payload;
            try
            {
                payload = BuildPayload(title, description, color, fields, imageUrl);
            }
            catch
            {
                return;
            }

            await PostPayloadAsync(activeUrl, payload).ConfigureAwait(false);
        }

        /// <summary>
        /// Posts a moderation action as a new post in a Discord FORUM channel. The post title
        /// is the player's name; the body lists the username, reason, action taken, and their
        /// VRChat profile link. Only call this for actual bans/moderation actions. No-op unless
        /// a forum webhook is bound to the active account.
        /// </summary>
        public async Task PostModerationForumAsync(string playerName, string userId, string reason, string actionTaken, string? actionedBy = null)
        {
            if (!IsForumEnabled)
                return;

            var safeName = string.IsNullOrWhiteSpace(playerName) ? "Unknown player" : playerName.Trim();
            var safeReason = string.IsNullOrWhiteSpace(reason) ? "(no reason provided)" : reason.Trim();
            var safeAction = string.IsNullOrWhiteSpace(actionTaken) ? "Moderated" : actionTaken.Trim();
            var safeActionedBy = string.IsNullOrWhiteSpace(actionedBy) ? "Unknown staff member" : actionedBy.Trim();
            var profileUrl = string.IsNullOrWhiteSpace(userId)
                ? "(unknown profile)"
                : $"https://vrchat.com/home/user/{userId.Trim()}";

            var content =
                $"**Username:** {safeName}\n\n" +
                $"**Reason for removal:** {safeReason}\n\n" +
                $"**Action taken:** {safeAction}\n\n" +
                $"**Actioned by:** {safeActionedBy}\n\n" +
                $"**Full Profile URL:** {profileUrl}";

            // thread_name turns this into a NEW forum post titled with the player's name.
            var payload = new JObject
            {
                ["username"] = "VRChat Group Auto Moderation",
                ["thread_name"] = Truncate(safeName, 100),
                ["content"] = Truncate(content, 2000)
            };

            await PostPayloadAsync(activeForumUrl, payload).ConfigureAwait(false);
        }

        // Serialized POST to a webhook URL with one retry that honors a 429 Retry-After delay.
        private async Task PostPayloadAsync(string targetUrl, JObject payload)
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
                return;

            await sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // One retry after honoring a 429 Retry-After delay.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    using var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                    using var response = await http.PostAsync(targetUrl, content).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                        return;

                    if ((int)response.StatusCode == 429 && attempt == 0)
                    {
                        var waitMs = await ReadRetryAfterMsAsync(response).ConfigureAwait(false);
                        await Task.Delay(waitMs).ConfigureAwait(false);
                        continue;
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (body.Length > 200)
                        body = body.Substring(0, 200) + "...";
                    OnLog?.Invoke($"[WEBHOOK] Discord returned {(int)response.StatusCode}. {body}");
                    return;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[WEBHOOK] Failed to send alert: {ex.Message}");
            }
            finally
            {
                sendGate.Release();
            }
        }

        private static async Task<int> ReadRetryAfterMsAsync(HttpResponseMessage response)
        {
            // Discord returns retry_after (seconds, may be fractional) in the JSON body.
            try
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var parsed = JObject.Parse(body);
                    var retryAfter = parsed["retry_after"]?.Value<double>();
                    if (retryAfter is > 0)
                        return Math.Min(10000, (int)Math.Ceiling(retryAfter.Value * 1000) + 100);
                }
            }
            catch
            {
                // Fall through to a safe default.
            }

            if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta.TotalMilliseconds > 0)
                return Math.Min(10000, (int)delta.TotalMilliseconds + 100);

            return 1500;
        }

        private static JObject BuildPayload(
            string title,
            string? description,
            int color,
            IEnumerable<(string Name, string Value, bool Inline)>? fields,
            string? imageUrl)
        {
            var embed = new JObject
            {
                ["title"] = Truncate(title, 256),
                ["color"] = color,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
                ["footer"] = new JObject
                {
                    ["text"] = "VRChat Group Auto Moderation"
                }
            };

            if (!string.IsNullOrWhiteSpace(description))
                embed["description"] = Truncate(description, 4096);

            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) &&
                imageUri.Scheme == Uri.UriSchemeHttps)
            {
                embed["image"] = new JObject { ["url"] = imageUri.ToString() };
            }

            if (fields != null)
            {
                var fieldArray = new JArray();
                foreach (var field in fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Name) || string.IsNullOrWhiteSpace(field.Value))
                        continue;

                    fieldArray.Add(new JObject
                    {
                        ["name"] = Truncate(field.Name, 256),
                        ["value"] = Truncate(field.Value, 1024),
                        ["inline"] = field.Inline
                    });

                    if (fieldArray.Count == 25)
                        break;
                }

                if (fieldArray.Count > 0)
                    embed["fields"] = fieldArray;
            }

            return new JObject
            {
                ["username"] = "VRChat Group Auto Moderation",
                ["embeds"] = new JArray { embed }
            };
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return value.Length <= max ? value : value.Substring(0, max - 1) + "\u2026";
        }
    }

    /// <summary>Outcome of binding a webhook to a logged-in account.</summary>
    public enum WebhookBindResult
    {
        /// <summary>No webhook is set for this account, and none was available to adopt.</summary>
        NotConfigured,
        /// <summary>A webhook was already bound to this account and is now active.</summary>
        Existing,
        /// <summary>The URL from webhook_config.txt was adopted and bound to this account.</summary>
        Adopted
    }
}
