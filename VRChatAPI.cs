using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace VRChatInstanceLogger
{
    public class VRChatAPI
    {
        private readonly HttpClient client;
        private readonly ConcurrentDictionary<string, bool> groupStaffCheckCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> groupStaffRoleCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<string>> groupStaffRoleIdsCache = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, bool> userGroupMembershipCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> groupNameCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, bool> groupBanStatusCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, UserGroupSnapshot> userGroupSnapshotCache = new ConcurrentDictionary<string, UserGroupSnapshot>(StringComparer.OrdinalIgnoreCase);
        private DateTime apiCooldownUntilUtc = DateTime.MinValue;
        private DateTime lastApiNoticeUtc = DateTime.MinValue;
        private readonly System.Threading.SemaphoreSlim requestThrottleGate = new System.Threading.SemaphoreSlim(1, 1);
        private DateTime lastRequestStartedUtc = DateTime.MinValue;
        private static readonly TimeSpan minRequestInterval = TimeSpan.FromMilliseconds(1200);
        public string? LastLoadedGroupBlacklistPath { get; private set; }
        public event Action<string>? OnApiInfo;
        public event Action<int>? OnApiCooldownStarted;

        private sealed class UserGroupSnapshot
        {
            public HashSet<string> GroupIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> MembershipIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public DateTime ExpiresAtUtc { get; init; }
        }

        public HttpClient GetHttpClient() => client;
        public bool IsCoolingDown => DateTime.UtcNow < apiCooldownUntilUtc;

        public VRChatAPI(string email, string password, CookieContainer? cookieContainer = null)
        {
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer ?? new CookieContainer()
            };

            client = new HttpClient(handler);

            // Prefer authenticated cookie sessions (including 2FA) when available.
            // Sending Basic auth alongside a valid cookie session can cause instance
            // endpoints to reject access for private instances.
            if (cookieContainer == null &&
                !string.IsNullOrWhiteSpace(email) &&
                !string.IsNullOrWhiteSpace(password))
            {
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            }

            client.DefaultRequestHeaders.UserAgent.ParseAdd("VRChat Group Auto Moderation/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
        }

        // Paces outbound API requests so a crowded instance does not burst dozens of
        // calls at once and trip VRChat's rate limit (which forces a cooldown).
        private async Task ThrottleAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            await requestThrottleGate.WaitAsync(cancellationToken);
            try
            {
                var elapsed = DateTime.UtcNow - lastRequestStartedUtc;
                if (elapsed < minRequestInterval)
                    await Task.Delay(minRequestInterval - elapsed, cancellationToken);
                lastRequestStartedUtc = DateTime.UtcNow;
            }
            finally
            {
                requestThrottleGate.Release();
            }
        }

        // -----------------------------
        // GET INSTANCE INFO
        // -----------------------------
        public async Task<JObject> GetInstanceAsync(string worldId, string instanceId)
        {
            string escapedWorldId = Uri.EscapeDataString(worldId);
            string escapedInstanceId = Uri.EscapeDataString(instanceId);
            string combinedEscaped = Uri.EscapeDataString($"{worldId}:{instanceId}");

            // Some VRChat routes reject encoded instance tokens while others require them.
            // Try several known endpoint shapes and token formats.
            var urls = new List<string>
            {
                $"https://api.vrchat.cloud/api/1/instances/{escapedInstanceId}",
                $"https://api.vrchat.cloud/api/1/instances/{combinedEscaped}",
                $"https://api.vrchat.cloud/api/1/instances/{instanceId}",
                $"https://api.vrchat.cloud/api/1/worlds/{escapedWorldId}/instances/{escapedInstanceId}",
                $"https://api.vrchat.cloud/api/1/worlds/{escapedWorldId}/instances/{instanceId}",
                $"https://api.vrchat.cloud/api/1/worlds/{escapedWorldId}/{escapedInstanceId}",
                $"https://api.vrchat.cloud/api/1/worlds/{escapedWorldId}/{instanceId}",
                $"https://api.vrchat.cloud/api/1/instances/{escapedWorldId}:{escapedInstanceId}"
            };

            string lastError = "Unknown instance lookup error.";
            JObject? firstSuccessWithoutUsers = null;
            string? firstSuccessWithoutUsersUrl = null;

            foreach (var url in urls)
            {
                try
                {
                    await ThrottleAsync();
                    using var response = await client.GetAsync(url);
                    var body = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var parsed = JObject.Parse(body);

                        // Some endpoints return lightweight instance metadata without
                        // the users list. Keep trying until we find a payload with users.
                        if (parsed["users"] is JArray)
                            return parsed;

                        firstSuccessWithoutUsers ??= parsed;
                        firstSuccessWithoutUsersUrl ??= url;
                        lastError = $"Instance response from {url} did not include a users array.";
                        continue;
                    }

                    var snippet = string.IsNullOrWhiteSpace(body)
                        ? response.ReasonPhrase ?? "No response body"
                        : body;

                    if (snippet.Length > 220)
                        snippet = snippet.Substring(0, 220) + "...";

                    lastError = $"Failed to fetch instance (HTTP {(int)response.StatusCode}) using {url}: {snippet}";

                    // Try the alternate endpoint shape after 400/404/405.
                    if ((int)response.StatusCode == 400 || (int)response.StatusCode == 404 || (int)response.StatusCode == 405)
                        continue;

                    break;
                }
                catch (Exception ex)
                {
                    lastError = $"Failed to fetch instance using {url}: {ex.Message}";
                }
            }

            if (firstSuccessWithoutUsers != null)
            {
                firstSuccessWithoutUsers["_instanceNotice"] = $"Instance payload from {firstSuccessWithoutUsersUrl} did not include users array.";
                return firstSuccessWithoutUsers;
            }

            return JObject.FromObject(new
            {
                error = true,
                message = lastError
            });
        }

        // -----------------------------
        // GET USER INFO
        // -----------------------------
        public async Task<JObject> GetUserAsync(string userId)
        {
            string url = $"https://api.vrchat.cloud/api/1/users/{userId}";

            try
            {
                await ThrottleAsync();
                string json = await client.GetStringAsync(url);
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new
                {
                    error = true,
                    message = $"Failed to fetch user: {ex.Message}"
                });
            }
        }

        public async Task<JObject> GetAuthenticatedUserAsync()
        {
            const string url = "https://api.vrchat.cloud/api/1/auth/user";

            try
            {
                string json = await client.GetStringAsync(url);
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new
                {
                    error = true,
                    message = $"Failed to fetch authenticated user: {ex.Message}"
                });
            }
        }

        public async Task<JObject> GetAvatarAsync(string avatarId)
        {
            if (string.IsNullOrWhiteSpace(avatarId) || !avatarId.StartsWith("avtr_", StringComparison.OrdinalIgnoreCase))
                return JObject.FromObject(new { error = true, message = "Invalid avatar ID." });

            try
            {
                await ThrottleAsync();
                using var response = await client.GetAsync($"https://api.vrchat.cloud/api/1/avatars/{Uri.EscapeDataString(avatarId)}");
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return JObject.FromObject(new { error = true, message = $"Avatar lookup failed (HTTP {(int)response.StatusCode})." });

                return JObject.Parse(body);
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new { error = true, message = $"Failed to fetch avatar: {ex.Message}" });
            }
        }

        public async Task<byte[]?> GetImageBytesAsync(string imageUrl)
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
                imageUri.Scheme != Uri.UriSchemeHttps ||
                !IsAllowedImageHost(imageUri.Host))
                return null;

            try
            {
                await ThrottleAsync();
                using var response = await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > 8 * 1024 * 1024)
                    return null;

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                while (buffer.Length <= 8 * 1024 * 1024)
                {
                    var read = await stream.ReadAsync(chunk);
                    if (read == 0)
                        return buffer.ToArray();

                    if (buffer.Length + read > 8 * 1024 * 1024)
                        return null;

                    await buffer.WriteAsync(chunk.AsMemory(0, read));
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAllowedImageHost(string host)
            => host.Equals("api.vrchat.cloud", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".vrchat.cloud", StringComparison.OrdinalIgnoreCase)
                || host.Equals("vrchat.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".vrchat.com", StringComparison.OrdinalIgnoreCase);

        // -----------------------------
        // GET WORLD INFO
        // -----------------------------
        public async Task<JObject> GetWorldAsync(string worldId)
        {
            string url = $"https://api.vrchat.cloud/api/1/worlds/{worldId}";

            try
            {
                string json = await client.GetStringAsync(url);
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new
                {
                    error = true,
                    message = $"Failed to fetch world: {ex.Message}"
                });
            }
        }

        public async Task<JObject> GetGroupAsync(string groupId)
        {
            string url = $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}";

            try
            {
                string json = await client.GetStringAsync(url);
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new
                {
                    error = true,
                    message = $"Failed to fetch group: {ex.Message}"
                });
            }
        }

        // Returns the most recent group audit-log entries (newest first) as a JArray.
        // Requires the authenticated account to have audit-view permission on the group.
        // Returns an empty array on any error so callers can poll safely.
        public async Task<JArray> GetGroupAuditLogsAsync(string groupId, int n = 50)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return new JArray();

            var url = $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}/auditLogs?n={n}";

            try
            {
                var json = await client.GetStringAsync(url);
                var parsed = JObject.Parse(json);
                return parsed["results"] as JArray ?? new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        // Returns true only when the given user is the confirmed owner of the group.
        // Uses a single group fetch and compares the group's ownerId. Any error, private
        // group, or missing ownerId results in false so callers fail closed.
        public async Task<bool> IsGroupOwnedByUserAsync(string groupId, string userId)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId))
                return false;

            var group = await GetGroupAsync(groupId);
            if (group["error"] != null)
                return false;

            var ownerId = group["ownerId"]?.ToString();
            return !string.IsNullOrWhiteSpace(ownerId)
                && string.Equals(ownerId, userId, StringComparison.OrdinalIgnoreCase);
        }

        public List<string> LoadConfiguredBlacklistedGroupIds()
        {
            var result = LoadBlacklistedGroupIdsFromDisk(out var loadedPath);
            LastLoadedGroupBlacklistPath = loadedPath;
            return result;
        }

        // Reads the configured blacklisted group ids straight from disk. Does not require
        // authentication, so the UI can show the loaded list before logging starts.
        public static List<string> LoadBlacklistedGroupIdsFromDisk(out string? loadedPath)
        {
            var result = new List<string>();
            loadedPath = null;

            // Only look right next to the running app. Using the working directory or
            // parent folders could let the app pick up an unrelated list (for example a
            // build placed in a subfolder of a folder that already has lists), so the
            // lookup is intentionally limited to the executable's own directory.
            var baseDir = RuntimePaths.Root;

            var preferredPath = Path.Combine(baseDir, "group_blacklist.txt.txt");
            var legacyPath = Path.Combine(baseDir, "group_blacklist.txt");

            var selectedPath = File.Exists(preferredPath) ? preferredPath
                               : File.Exists(legacyPath) ? legacyPath
                               : null;

            if (string.IsNullOrWhiteSpace(selectedPath))
                return result;

            loadedPath = selectedPath;

            foreach (var raw in File.ReadAllLines(selectedPath))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var match = System.Text.RegularExpressions.Regex.Match(line, "((?:grp|gmem)_[A-Za-z0-9_-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                var groupId = match.Groups[1].Value;
                if (!result.Contains(groupId, StringComparer.OrdinalIgnoreCase))
                    result.Add(groupId);
            }

            return result;
        }

        // Loads and caches a user's group snapshot in a single API call so a full
        // blacklist scan can then be evaluated locally without per-group network calls.
        public async Task<bool> TryCacheUserGroupsAsync(string userId, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var snapshot = await GetUserGroupSnapshotAsync(userId, cancellationToken);
            return snapshot != null;
        }

        public async Task<bool> IsUserInGroupAsync(string userId, string groupId, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(groupId))
                return false;

            if (IsCoolingDown)
            {
                NotifyCooldownSkip("group membership lookup");
                return false;
            }

            var normalizedTarget = groupId.Trim();
            var isMembershipIdentifier = normalizedTarget.StartsWith("gmem_", StringComparison.OrdinalIgnoreCase);
            var isGroupIdentifier = normalizedTarget.StartsWith("grp_", StringComparison.OrdinalIgnoreCase);

            var cacheKey = $"{userId}:{groupId}";
            if (userGroupMembershipCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var snapshot = await GetUserGroupSnapshotAsync(userId, cancellationToken);
                if (snapshot != null)
                {
                    var isMatch = false;
                    if (isMembershipIdentifier)
                    {
                        isMatch = snapshot.MembershipIds.Contains(normalizedTarget);
                    }
                    else if (isGroupIdentifier)
                    {
                        isMatch = snapshot.GroupIds.Contains(normalizedTarget);
                    }
                    else
                    {
                        isMatch = snapshot.GroupIds.Contains(normalizedTarget) || snapshot.MembershipIds.Contains(normalizedTarget);
                    }

                    userGroupMembershipCache[cacheKey] = isMatch;
                    return isMatch;
                }

                // Snapshot unavailable (cooldown or fetch failure). Do NOT fall back to
                // per-group membership lookups: that multiplies API calls by the number
                // of blacklisted groups and trips the rate limit. Treat as no match.
                return false;
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fail-open with retry opportunity on next pass.
                return false;
            }
        }

        private async Task<UserGroupSnapshot?> GetUserGroupSnapshotAsync(string userId, System.Threading.CancellationToken cancellationToken)
        {
            if (userGroupSnapshotCache.TryGetValue(userId, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
                return cached;

            if (IsCoolingDown)
            {
                NotifyCooldownSkip("user group snapshot");
                return null;
            }

            var url = $"https://api.vrchat.cloud/api/1/users/{Uri.EscapeDataString(userId)}/groups";
            await ThrottleAsync(cancellationToken);
            var response = await client.GetAsync(url, cancellationToken);
            ApplyCooldownIfNeeded(response, "user group snapshot");
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var groups = JArray.Parse(content);

            var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var membershipIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in groups.OfType<JObject>())
            {
                var membershipId = token["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(membershipId) && membershipId.StartsWith("gmem_", StringComparison.OrdinalIgnoreCase))
                    membershipIds.Add(membershipId);

                var groupId = token["groupId"]?.ToString() ?? token["group"]?["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(groupId) && groupId.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                    groupIds.Add(groupId);
            }

            var snapshot = new UserGroupSnapshot
            {
                GroupIds = groupIds,
                MembershipIds = membershipIds,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(1)
            };

            userGroupSnapshotCache[userId] = snapshot;
            return snapshot;
        }

        public async Task<string?> GetGroupNameAsync(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return null;

            if (groupNameCache.TryGetValue(groupId, out var cached))
                return cached;

            var group = await GetGroupAsync(groupId);
            if (group["error"] != null)
                return null;

            var name = group["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                groupNameCache[groupId] = name;

            return name;
        }

        // -----------------------------
        // BAN USER FROM GROUP
        // -----------------------------
        public async Task<(bool success, string error)> BanUserFromGroup(string groupId, string userId)
        {
            try
            {
                // Try POST to bans endpoint with userId in body for actual ban (prevents rejoin)
                var requestBody = new JObject
                {
                    ["userId"] = userId
                };
                var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"https://api.vrchat.cloud/api/1/groups/{groupId}/bans",
                    content
                );

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    groupBanStatusCache[$"{groupId}:{userId}"] = true;
                    return (true, "");
                }
                else
                {
                    if (responseContent.Contains("already banned", StringComparison.OrdinalIgnoreCase))
                        groupBanStatusCache[$"{groupId}:{userId}"] = true;

                    return (false, $"HTTP {response.StatusCode}: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }

        // -----------------------------
        // UNBAN USER FROM GROUP
        // -----------------------------
        public async Task<(bool success, string error)> UnbanUserFromGroup(string groupId, string userId)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId))
                return (false, "Missing group id or user id.");

            try
            {
                var response = await client.DeleteAsync(
                    $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}/bans/{Uri.EscapeDataString(userId)}"
                );

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Success, or the user was not banned to begin with — either way they are
                    // no longer banned. Clear any cached "banned" status.
                    groupBanStatusCache[$"{groupId}:{userId}"] = false;
                    return (true, "");
                }

                return (false, $"HTTP {response.StatusCode}: {responseContent}");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }

        public async Task<bool> IsUserBannedFromGroupAsync(string groupId, string userId, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId))
                return false;

            if (IsCoolingDown)
            {
                NotifyCooldownSkip("group ban lookup");
                return false;
            }

            var cacheKey = $"{groupId}:{userId}";
            if (groupBanStatusCache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                // Primary endpoint: returns a ban record when user is banned.
                var directUrl = $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}/bans/{Uri.EscapeDataString(userId)}";
                await ThrottleAsync(cancellationToken);
                var directResponse = await client.GetAsync(directUrl, cancellationToken);
                ApplyCooldownIfNeeded(directResponse, "group ban direct lookup");

                if (directResponse.IsSuccessStatusCode)
                {
                    groupBanStatusCache[cacheKey] = true;
                    return true;
                }

                // Common non-banned response for direct lookup.
                if (directResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    groupBanStatusCache[cacheKey] = false;
                    return false;
                }

                // Fallback endpoint shape seen in some API deployments.
                var queryUrl = $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}/bans?userId={Uri.EscapeDataString(userId)}";
                await ThrottleAsync(cancellationToken);
                var queryResponse = await client.GetAsync(queryUrl, cancellationToken);
                ApplyCooldownIfNeeded(queryResponse, "group ban query lookup");
                if (!queryResponse.IsSuccessStatusCode)
                {
                    groupBanStatusCache[cacheKey] = false;
                    return false;
                }

                var body = await queryResponse.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(body))
                {
                    groupBanStatusCache[cacheKey] = false;
                    return false;
                }

                var parsed = JToken.Parse(body);
                var isBanned = parsed is JArray arr ? arr.Count > 0 : parsed["userId"] != null;
                groupBanStatusCache[cacheKey] = isBanned;
                return isBanned;
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fail-open to avoid blocking moderation on transient endpoint issues.
                groupBanStatusCache[cacheKey] = false;
                return false;
            }
        }

        private void ApplyCooldownIfNeeded(HttpResponseMessage response, string operation)
        {
            if (response == null)
                return;

            // Only HTTP 429 (Too Many Requests) is a genuine rate-limit cooldown.
            // 401/403 are auth/permission responses that occur normally when querying
            // groups we don't own or moderate, and must NOT trigger a cooldown.
            var statusCode = (int)response.StatusCode;
            if (statusCode != 429)
                return;

            var seconds = 30;
            if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta.TotalSeconds > 0)
                seconds = Math.Max(5, (int)Math.Ceiling(delta.TotalSeconds));

            var until = DateTime.UtcNow.AddSeconds(seconds);
            var cooldownExtended = until > apiCooldownUntilUtc;
            if (cooldownExtended)
                apiCooldownUntilUtc = until;

            if (cooldownExtended)
                OnApiCooldownStarted?.Invoke(seconds);

            if (DateTime.UtcNow - lastApiNoticeUtc > TimeSpan.FromSeconds(10))
            {
                lastApiNoticeUtc = DateTime.UtcNow;
                OnApiInfo?.Invoke($"[API] Cooldown active for ~{seconds}s after {operation} returned {(int)response.StatusCode}.");
            }
        }

        private void NotifyCooldownSkip(string operation)
        {
            if (DateTime.UtcNow - lastApiNoticeUtc > TimeSpan.FromSeconds(10))
            {
                lastApiNoticeUtc = DateTime.UtcNow;
                var remainingSeconds = apiCooldownUntilUtc > DateTime.UtcNow
                    ? (int)Math.Ceiling((apiCooldownUntilUtc - DateTime.UtcNow).TotalSeconds)
                    : 0;
                OnApiInfo?.Invoke($"[API] Skipping {operation}; cooldown remaining ~{remainingSeconds}s.");
            }
        }

        public async Task<bool> IsUserGroupStaffAsync(string groupId, string userId)
        {
            return !string.IsNullOrWhiteSpace(await GetUserGroupStaffRoleAsync(groupId, userId));
        }

        public async Task<string?> GetUserGroupStaffRoleAsync(string groupId, string userId)
        {
            if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(userId))
                return null;

            var cacheKey = $"{groupId}:{userId}";
            if (groupStaffRoleCache.TryGetValue(cacheKey, out var cachedRole))
                return string.IsNullOrWhiteSpace(cachedRole) ? null : cachedRole;

            try
            {
                var url = $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}/members/{Uri.EscapeDataString(userId)}";
                await ThrottleAsync();
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    groupStaffCheckCache[cacheKey] = false;
                    groupStaffRoleCache[cacheKey] = string.Empty;
                    return null;
                }

                var member = JObject.Parse(await response.Content.ReadAsStringAsync());
                var role = member["isOwner"]?.Value<bool>() == true ? "OWNER" :
                    member["isAdmin"]?.Value<bool>() == true ? "ADMIN" :
                    member["isModerator"]?.Value<bool>() == true ? "MOD" : null;

                var roleIds = member["roleIds"] as JArray;
                if (role == null && roleIds != null && roleIds.Count > 0)
                {
                    var staffRoleIds = await GetStaffRoleIdsAsync(groupId);
                    if (roleIds.Any(token => staffRoleIds.Contains(token?.ToString() ?? string.Empty)))
                        role = "STAFF";
                }

                groupStaffCheckCache[cacheKey] = role != null;
                groupStaffRoleCache[cacheKey] = role ?? string.Empty;
                return role;
            }
            catch
            {
                groupStaffCheckCache[cacheKey] = false;
                groupStaffRoleCache[cacheKey] = string.Empty;
                return null;
            }
        }

        private async Task<HashSet<string>> GetStaffRoleIdsAsync(string groupId)
        {
            if (groupStaffRoleIdsCache.TryGetValue(groupId, out var cached))
                return cached;

            var staffRoleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var url = $"https://api.vrchat.cloud/api/1/groups/{Uri.EscapeDataString(groupId)}/roles";
                await ThrottleAsync();
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    groupStaffRoleIdsCache[groupId] = staffRoleIds;
                    return staffRoleIds;
                }

                var json = await response.Content.ReadAsStringAsync();
                JToken token = JToken.Parse(json);

                JArray roles;
                if (token is JArray directArray)
                    roles = directArray;
                else
                    roles = token["roles"] as JArray ?? new JArray();

                foreach (var roleToken in roles)
                {
                    if (roleToken is not JObject role)
                        continue;

                    var roleId = role["id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(roleId))
                        continue;

                    var isManagementRole = role["isManagementRole"]?.Value<bool>() == true;

                    // Restrict exemptions to explicit group management roles only.
                    if (isManagementRole)
                        staffRoleIds.Add(roleId);
                }
            }
            catch
            {
                // Keep empty cache on failure to avoid repeated noisy network calls.
            }

            groupStaffRoleIdsCache[groupId] = staffRoleIds;
            return staffRoleIds;
        }
    }
}
