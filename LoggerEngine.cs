using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VRChatInstanceLogger
{
    public class LoggerEngine
    {
        private readonly VRChatAPI api;
        private readonly InstanceDetector detector;

        private readonly HashSet<string> knownPlayers = new HashSet<string>();
        private readonly Dictionary<string, string> userIdToDisplayName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> processedAutoBanUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> restrictedAreaMarkers;
        private readonly List<string> ripSuspicionMarkers;
        private readonly List<string> configuredBlacklistedGroupIds;
        private readonly HashSet<string> configuredWhitelistedGroupIds;
        private readonly List<string> configuredBlacklistedAvatars;
        private readonly HashSet<string> processedAvatarBanUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Blacklisted avatars seen before the wearer's user id was known, keyed by display
        // name -> matched avatar. Resolved and banned once the player's join line arrives.
        private readonly Dictionary<string, string> pendingAvatarBans = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Each present player's currently-worn avatar, keyed by display name. Lets a newly
        // added blacklist entry be checked against everyone already in the instance.
        private readonly Dictionary<string, (string? Name, string? Id)> currentAvatarByName = new Dictionary<string, (string? Name, string? Id)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> lastReportedAvatarIdByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string banTargetGroupId;

        // The group players are auto-banned from. Exposed so the UI can offer an "Unban"
        // action for players that were banned during this session.
        public string BanTargetGroupId => banTargetGroupId;
        // The current instance's world id (wrld_...), for the UI's world-info tab.
        public string? CurrentWorldId => lastLoggedWorldId;
        public string? CurrentInstanceId => lastLoggedInstanceId;
        private readonly HashSet<string> staffUserIds;
        private readonly HashSet<string> staffDisplayNames;
        private readonly HashSet<string> staffGroupIds;
        private readonly HashSet<string> ownedGroupIds;
        private readonly HashSet<string> runtimeStaffUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> runtimeStaffGroupNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> runtimeStaffRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> dynamicallyDetectedRestrictedAreas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedRestrictedMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedRipSuspicionMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedUdonDetections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedButtonPressDetections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedModernUiDetections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<DateTime> recentUdonExceptions = new List<DateTime>();
        private readonly List<DateTime> recentUdonInteractions = new List<DateTime>();
        private readonly Dictionary<string, Queue<DateTime>> recentPickupEventsByUser = new Dictionary<string, Queue<DateTime>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> reportedPickupBurstUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastUnattributedPickupWarningUtc = DateTime.MinValue;
        private static readonly TimeSpan udonFloodWindow = TimeSpan.FromSeconds(10);
        private const int udonFloodThreshold = 25;
        private const int udonInteractionThreshold = 12;
        private static readonly TimeSpan pickupBurstWindow = TimeSpan.FromSeconds(8);
        private const int pickupBurstThreshold = 18;
        private const int pickupLogBatchSize = 5;
        // Built-in malicious/crasher Udon indicators (case-insensitive substrings).
        private static readonly string[] udonExploitMarkers =
        {
            "udon crasher",
            "malicious udon",
            "udon exploit",
            "udon heap overflow",
            "udon stack overflow",
            "udon out of memory",
            "spam udon",
            "crash lobby",
            "lobby crash",
            "e1 spam",
            "item orbit",
            "spam item orbit",
            "orbit spam",
            "button spam",
            "udon spam",
            "crash button",
            "malicious button",
            "interaction spam",
            "object orbit",
            "item spam",
            "toggle spam",
            "menu spam"
        };
        private static readonly string[] udonActivityMarkers =
        {
            "udonbehaviour",
            "udon behaviour",
            "udonsharp",
            "udon sharp",
            "udon program",
            "udon event",
            "udon script",
            "udon runtime",
            "button pressed",
            "button click",
            "button press",
            "pressed button",
            "clicked button",
            "triggered button",
            "button interaction",
            "button action",
            "ui button",
            "onbutton",
            "toggle fireworks show",
            "fireworks show",
            "call security",
            "toggle drum",
            "toggle drums",
            "show message",
            "message panel",
            "toggle post processing",
            "toggle reflections",
            "toggle particles",
            "toggle table colliders",
            "toggle furniture colliders",
            "toggle seats",
            "override dynamic speed",
            "toggle video player",
            "toggle drunk audio",
            "activate voice ducking",
            "show voice ducking area of effect",
            "activate microphone",
            "toggle stage dj deck",
            "do not serve",
            "prevent pickup",
            "toggle menu locks",
            "toggle menu gravity",
            "toggle log",
            "verify user",
            "verify user vip",
            "dumpster player",
            "lockout user",
            "jail user",
            "security panel",
            "management panel",
            "reimajo",
            "reimajo admin panel",
            "reimajo panel",
            "room master",
            "give gen. manager",
            "fallback gen. manager",
            "assistant manager",
            "bartender",
            "stage performer",
            "dj / music",
            "security",
            "fireworks",
            "drum",
            "picked up",
            "pickup",
            "item pickup",
            "onpickup",
            "triggered event",
            "interaction"
        };
        private static readonly string[] udonWorldObjectMarkers =
        {
            "udonbehaviour",
            "udon behaviour",
            "udonsharp",
            "udonsharpbehaviour",
            "udon program source",
            "udon programsource",
            "program source",
            "vrc.udon.udonbehaviour",
            "vrc.udon",
            "udon script",
            "udon runtime",
            "udon event",
            "udon object",
            "game object has udon",
            "world contains udon",
            "udon script attached",
            "udonsharp behaviour"
        };
        private static readonly string[] udonButtonPressMarkers =
        {
            "button pressed",
            "button click",
            "button press",
            "clicked button",
            "pressed button",
            "button interaction",
            "button action",
            "button trigger",
            "onbutton",
            "toggle post processing",
            "toggle reflections",
            "toggle particles",
            "toggle table colliders",
            "toggle furniture colliders",
            "toggle seats",
            "override dynamic speed",
            "toggle video player",
            "toggle drunk audio",
            "activate voice ducking",
            "show voice ducking area of effect",
            "activate microphone",
            "toggle stage dj deck",
            "do not serve",
            "prevent pickup",
            "toggle menu locks",
            "toggle menu gravity",
            "toggle log",
            "verify user",
            "verify user vip",
            "dumpster player",
            "lockout user",
            "jail user",
            "call security",
            "toggle drums",
            "toggle drum",
            "toggle fireworks show",
            "show message",
            "security panel",
            "management panel",
            "reimajo",
            "reimajo admin panel",
            "reimajo panel",
            "give gen. manager",
            "fallback gen. manager",
            "assistant manager",
            "bartender",
            "stage performer",
            "dj / music",
            "security",
            "room master"
        };
        private const string udonExceptionSignature = "an exception occurred during udon execution";
        // Force-teleport-to-ban-zone detection state (world ban prefabs like "ModernUIs").
        private readonly HashSet<string> reportedForceTeleports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Players already scanned for flagged profile watchwords this instance.
        private readonly HashSet<string> flaggedProfileUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string lastBanListActorName = string.Empty;
        private DateTime lastBanListActorAtUtc = DateTime.MinValue;
        private static readonly TimeSpan forceTeleportWindow = TimeSpan.FromSeconds(8);
        private static readonly Regex banListActorRegex = new Regex(@"\]\s*(.+?)\s+has sent network data on Banned Players", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly Dictionary<string, List<RestrictedAreaConfig>> worldRestrictedAreas = new Dictionary<string, List<RestrictedAreaConfig>>(StringComparer.OrdinalIgnoreCase);
        private readonly object playerStateLock = new object();
        private readonly Dictionary<string, DateTime> lastJoinLogAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> lastLeftLogAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> lastGroupCheckAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan duplicateUserEventWindow = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan duplicateGroupCheckWindow = TimeSpan.FromSeconds(20);
        private const int maxConcurrentPlayerChecks = 2;
        private DateTime rejoinTransitionUntilUtc = DateTime.MinValue;
        private string? authenticatedUserId;
        private string? authenticatedDisplayName;
        private string? lastLoggedWorldId;
        private readonly System.Threading.SemaphoreSlim playerProcessingSemaphore = new System.Threading.SemaphoreSlim(maxConcurrentPlayerChecks, maxConcurrentPlayerChecks);
        private string? lastLoggedInstanceId;
        private string? lastLoggedInstanceSignature;
        private bool usingLogUserFallback;
        private readonly Dictionary<string, System.Threading.CancellationTokenSource> playerCancellationTokens = new Dictionary<string, System.Threading.CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        private System.Threading.CancellationTokenSource? instancePollingCts;
        private System.Threading.CancellationTokenSource? detectorWatchingCts;
        private System.Threading.CancellationTokenSource? auditLogPollingCts;
        private DateTime lastAuditLogSeenUtc = DateTime.MinValue;
        private readonly HashSet<string> seenAuditLogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan auditLogPollInterval = TimeSpan.FromMinutes(3);
        private bool isStopped;

        private class RestrictedAreaConfig
        {
            public string? DetectionMethod { get; set; } // text, udon, coords
            public string? Pattern { get; set; }
            public string? Description { get; set; }
        }

        // A manual moderation action performed by a group staff member, read from the
        // group audit log (warn/kick/ban/unban).
        public class StaffModerationEvent
        {
            public string Actor { get; set; } = "Unknown staff";
            public string EventType { get; set; } = string.Empty;
            public string ActionLabel { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? TargetId { get; set; }
            public string? Location { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        // One player currently tracked in the instance, with enough context for the UI to
        // offer per-player actions (e.g. blacklist their current avatar).
        public class LobbyPlayer
        {
            public string DisplayName { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string? AvatarName { get; set; }
            public string? AvatarId { get; set; }
            public bool IsStaff { get; set; }
            public string? StaffGroupName { get; set; }
            public string? StaffRole { get; set; }
        }

        public class AvatarObservation
        {
            public string DisplayName { get; init; } = string.Empty;
            public string UserId { get; init; } = string.Empty;
            public string AvatarName { get; init; } = string.Empty;
            public string AvatarId { get; init; } = string.Empty;
            public string? ThumbnailUrl { get; init; }
            public byte[]? PreviewImage { get; init; }
        }

        public event Action<string>? OnLog;
        public event Action<string>? OnWorldChanged;
        public event Action<string, string>? OnPlayerJoined;
        public event Action<string, string>? OnPlayerLeft;
        public event Action<string, string, string>? OnPlayerBanned;
        public event Action<string, string, string>? OnBlacklistedGroupMatched;
        public event Action<string, string, string>? OnBlacklistedAvatarMatched;
        public event Action<AvatarObservation>? OnAvatarObserved;
        public event Action<StaffModerationEvent>? OnStaffModerationDetected;
        public event Action<string>? OnStatus;
        public event Action<IReadOnlyList<string>>? OnLobbyPlayersChanged;
        // Richer per-player lobby snapshot (name, id, current avatar, staff flag) for the UI.
        public event Action<IReadOnlyList<LobbyPlayer>>? OnLobbyPlayersDetailed;
        public event Action<string, int>? OnForcedStop;
        // (displayName, userId, marker) — userId/displayName may be empty when unknown.
        public event Action<string, string, string>? OnRipSuspicionDetected;
        // (displayName, userId, description) — a malicious/crasher Udon detection.
        public event Action<string, string, string>? OnUdonExploitDetected;
        // (displayName, userId, details) — a Modern UI panel detected in the instance or owned by a staff/moderator.
        public event Action<string, string, string>? OnModernUiPanelDetected;
        // (actorName, actorUserId) — a player who force-teleported the local user to a ban zone.
        public event Action<string, string>? OnForceTeleportDetected;
        // (displayName, matchedWord, worldName, groupName) — a profile flagged by a watchword.
        public event Action<string, string, string, string>? OnProfileFlagged;
        // (details) — a human-readable restricted-area detection description.
        public event Action<string>? OnRestrictedAreaDetected;

        public bool IsGroupModerationActive => configuredBlacklistedGroupIds != null && configuredBlacklistedGroupIds.Count > 0;
        public IReadOnlyList<string> ConfiguredBlacklistedGroupIds => configuredBlacklistedGroupIds;
        public IReadOnlyCollection<string> ConfiguredWhitelistedGroupIds => configuredWhitelistedGroupIds;
        public IReadOnlyList<string> ConfiguredBlacklistedAvatars => configuredBlacklistedAvatars;
        public IReadOnlyCollection<string> ConfiguredStaffGroupIds => staffGroupIds;

        // Adds a group id to the live staff-group list so members of that group are
        // treated as staff without restarting logging. Returns false if already present.
        public bool AddStaffGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            return staffGroupIds.Add(groupId);
        }

        // Removes a group id from the live staff-group list. Returns false if it was not
        // present.
        public bool RemoveStaffGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            return staffGroupIds.Remove(groupId);
        }

        public bool AddOwnedGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            return ownedGroupIds.Add(groupId);
        }

        public bool RemoveOwnedGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            return ownedGroupIds.Remove(groupId);
        }

        public bool AddWhitelistedGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            return configuredWhitelistedGroupIds.Add(groupId.Trim());
        }

        public bool RemoveWhitelistedGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            return configuredWhitelistedGroupIds.Remove(groupId.Trim());
        }

        // Adds a group id to the live blacklist so it takes effect without restarting
        // logging. Returns false if the id is already present.
        public bool AddBlacklistedGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            if (configuredBlacklistedGroupIds.Contains(groupId, StringComparer.OrdinalIgnoreCase))
                return false;
            if (configuredBlacklistedGroupIds.Count >= AccountBlacklist.MaxEntries)
                return false;

            configuredBlacklistedGroupIds.Add(groupId);
            return true;
        }

        // Removes a group id from the live blacklist. Returns false if it was not present.
        public bool RemoveBlacklistedGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            var index = configuredBlacklistedGroupIds.FindIndex(
                id => string.Equals(id, groupId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;

            configuredBlacklistedGroupIds.RemoveAt(index);
            return true;
        }

        public void ClearBlacklistedGroups()
        {
            configuredBlacklistedGroupIds.Clear();
        }

        // Adds an avatar name or id to the live blacklist so it takes effect without
        // restarting logging. Returns false if it is already present.
        public bool AddBlacklistedAvatar(string avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar))
                return false;

            var value = avatar.Trim();
            if (configuredBlacklistedAvatars.Contains(value, StringComparer.OrdinalIgnoreCase))
                return false;

            configuredBlacklistedAvatars.Add(value);
            // Catch anyone already in the instance who is wearing this avatar.
            _ = ScanPresentPlayersForAvatarAsync(value);
            return true;
        }

        // Removes an avatar name or id from the live blacklist. Returns false if it was
        // not present.
        public bool RemoveBlacklistedAvatar(string avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar))
                return false;

            var value = avatar.Trim();
            var index = configuredBlacklistedAvatars.FindIndex(
                a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;

            configuredBlacklistedAvatars.RemoveAt(index);
            return true;
        }

        public void ClearBlacklistedAvatars()
        {
            configuredBlacklistedAvatars.Clear();
        }

        public LoggerEngine(VRChatAPI api) : this(api, null)
        {
        }

        // When blacklistedGroupIds is provided (the per-account list), it is used as-is
        // instead of reading any shared file from disk, keeping each account isolated.
        public LoggerEngine(VRChatAPI api, IEnumerable<string>? blacklistedGroupIds)
            : this(api, blacklistedGroupIds, null)
        {
        }

        public LoggerEngine(VRChatAPI api, IEnumerable<string>? blacklistedGroupIds, IEnumerable<string>? blacklistedAvatars)
            : this(api, blacklistedGroupIds, blacklistedAvatars, null)
        {
        }

        public LoggerEngine(
            VRChatAPI api,
            IEnumerable<string>? blacklistedGroupIds,
            IEnumerable<string>? blacklistedAvatars,
            IEnumerable<string>? ownedGroupIds,
            IEnumerable<string>? whitelistedGroupIds = null)
        {
            this.api = api;
            this.detector = new InstanceDetector();
            this.restrictedAreaMarkers = LoadRestrictedAreaMarkers();
            this.ripSuspicionMarkers = LoadRipSuspicionMarkers();
            this.worldRestrictedAreas = LoadWorldRestrictedAreas();
            this.configuredBlacklistedGroupIds = blacklistedGroupIds != null
                ? blacklistedGroupIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : api.LoadConfiguredBlacklistedGroupIds();
            this.configuredBlacklistedAvatars = blacklistedAvatars != null
                ? blacklistedAvatars
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
            this.banTargetGroupId = LoadBanTargetGroupId();
            var staff = LoadGroupStaff();
            this.staffUserIds = staff.ids;
            this.staffDisplayNames = staff.names;
            this.staffGroupIds = LoadStaffGroups();
            this.ownedGroupIds = ownedGroupIds != null
                ? new HashSet<string>(ownedGroupIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.configuredWhitelistedGroupIds = whitelistedGroupIds != null
                ? new HashSet<string>(whitelistedGroupIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()), StringComparer.OrdinalIgnoreCase)
                : LoadWhitelistedGroupIds();
            api.OnApiCooldownStarted += HandleApiCooldownStarted;

            detector.OnDebug += message =>
            {
                CheckForRestrictedAreaInLogs(message);
                CheckForRipSuspicionInLogs(message);
                CheckForUdonExploitInLogs(message);
                CheckForPickupBurstInLogs(message);
                CheckForWorldButtonPressesInLogs(message);
                CheckForModernUiPanelInLogs(message);
                OnLog?.Invoke(message);
            };
            detector.OnPlayerJoined += async (displayName, userId) => await HandleObservedUserAsync(displayName, userId);
            detector.OnPlayerLeft += (displayName, userId) => HandlePlayerLeft(displayName, userId);
            detector.OnAvatarObserved += async (displayName, avatarName, avatarId) => await HandleObservedAvatarAsync(displayName, avatarName, avatarId);
            detector.OnLogLine += async line =>
            {
                CheckForRestrictedAreaInLogs(line);
                CheckForRipSuspicionInLogs(line);
                CheckForUdonExploitInLogs(line);
                CheckForPickupBurstInLogs(line);
                CheckForWorldButtonPressesInLogs(line);
                CheckForModernUiPanelInLogs(line);
                CheckForForceTeleportInLogs(line);
                await HandleRestrictedAreaLineAsync(line);
            };

            detector.OnInstanceChanged += async (worldId, instanceId) =>
            {
                await SwitchToInstanceAsync(worldId, instanceId, isStartupAttach: false);
            };
        }

        public async Task Start()
        {
            isStopped = false;
            string? authLocation = null;
            try
            {
                var authUser = await api.GetAuthenticatedUserAsync();
                if (authUser["error"] == null)
                {
                    authenticatedUserId = authUser["id"]?.ToString();
                    authenticatedDisplayName = authUser["displayName"]?.ToString();
                    authLocation = authUser["location"]?.ToString();
                }
            }
            catch
            {
                // Best effort only.
            }

            OnLog?.Invoke("Instance detector started.");
            OnLog?.Invoke("[INFO] Logging started. Detecting the current instance.");
            if (!string.IsNullOrWhiteSpace(api.LastLoadedGroupBlacklistPath))
                OnLog?.Invoke($"[GROUP-CHECK] Loaded blacklist file: {api.LastLoadedGroupBlacklistPath}");
            if (configuredBlacklistedGroupIds.Count > 0)
                OnLog?.Invoke($"[GROUP-CHECK] Loaded {configuredBlacklistedGroupIds.Count} blacklisted groups.");
            else
                OnLog?.Invoke("[GROUP-CHECK] No blacklisted groups configured. Group moderation is inactive.");
            if (ripSuspicionMarkers.Count > 0)
                OnLog?.Invoke($"[RIP-SUS] Auto detector enabled with {ripSuspicionMarkers.Count} marker(s) (built-in + custom). Action: log only/manual review.");
            else
                OnLog?.Invoke("[RIP-SUS] Detector is disabled (no markers configured).");
            OnLog?.Invoke($"[INFO] Udon exploit detector enabled ({udonExploitMarkers.Length} indicators + exception-flood watch).");
            OnLog?.Invoke("[FORCE-TP] Force-teleport detector enabled. Bans the player who teleports you to a world ban zone.");

            var instanceDetected = TryParseCurrentLocation(authLocation, out var worldId, out var instanceId);
            var detectionSource = "account session";
            if (!instanceDetected && detector.TryGetMostRecentInstance(out worldId, out instanceId))
            {
                instanceDetected = true;
                detectionSource = "recent VRChat log";
            }

            if (instanceDetected)
            {
                OnLog?.Invoke($"[INFO] Current instance detected from {detectionSource}. Starting checks now.");
                await SwitchToInstanceAsync(worldId, instanceId, isStartupAttach: true);
                OnStatus?.Invoke("Instance detected. Logging started.");
            }
            else
            {
                OnStatus?.Invoke("Waiting for a VRChat instance to be detected.");
            }

            detectorWatchingCts?.Cancel();
            detectorWatchingCts?.Dispose();
            detectorWatchingCts = new System.Threading.CancellationTokenSource();
            _ = detector.StartWatching(detectorWatchingCts.Token);

            auditLogPollingCts?.Cancel();
            auditLogPollingCts?.Dispose();
            auditLogPollingCts = new System.Threading.CancellationTokenSource();
            _ = StartAuditLogPollingLoopAsync(auditLogPollingCts.Token);
        }

        // Stops logging on user request: cancels all polling/detector/player tasks.
        public void Stop()
        {
            if (isStopped)
                return;

            StopInternal();
            OnLog?.Invoke("[INFO] Logging stopped.");
            OnStatus?.Invoke("Logging stopped.");
        }

        private void HandleApiCooldownStarted(int seconds)
        {
            if (isStopped)
                return;

            var reason = $"[API] Cooldown detected (~{seconds}s). Logging stopped. Press Start Logging again after cooldown.";
            OnLog?.Invoke(reason);
            OnStatus?.Invoke("Logging stopped by API cooldown. Press Start Logging again.");
            StopInternal();
            OnForcedStop?.Invoke("API cooldown started. Logging was stopped.", seconds);
        }

        private void StopInternal()
        {
            if (isStopped)
                return;

            isStopped = true;

            instancePollingCts?.Cancel();
            instancePollingCts?.Dispose();
            instancePollingCts = null;

            detectorWatchingCts?.Cancel();
            detectorWatchingCts?.Dispose();
            detectorWatchingCts = null;

            auditLogPollingCts?.Cancel();
            auditLogPollingCts?.Dispose();
            auditLogPollingCts = null;

            lock (playerStateLock)
            {
                foreach (var kv in playerCancellationTokens.Values)
                {
                    try { kv.Cancel(); } catch { }
                    kv.Dispose();
                }
                playerCancellationTokens.Clear();
            }
        }

        private async Task SwitchToInstanceAsync(string worldId, string instanceId, bool isStartupAttach)
        {
            var incomingSignature = BuildInstanceSignature(worldId, instanceId);
            if (string.Equals(lastLoggedInstanceSignature, incomingSignature, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lastLoggedWorldId = worldId;
            lastLoggedInstanceId = instanceId;
            lastLoggedInstanceSignature = incomingSignature;
            usingLogUserFallback = false;
            rejoinTransitionUntilUtc = DateTime.UtcNow.AddSeconds(8);

            var playersToLogLeft = new List<(string UserId, string DisplayName)>();
            lock (playerStateLock)
            {
                foreach (var trackedUserId in knownPlayers)
                {
                    if (string.Equals(trackedUserId, authenticatedUserId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var trackedName = userIdToDisplayName.TryGetValue(trackedUserId, out var knownName)
                        ? knownName
                        : "Unknown";

                    playersToLogLeft.Add((trackedUserId, trackedName));
                }

                knownPlayers.Clear();
                userIdToDisplayName.Clear();
                processedAutoBanUsers.Clear();
                processedAvatarBanUsers.Clear();
                runtimeStaffUserIds.Clear();
                runtimeStaffGroupNames.Clear();
                runtimeStaffRoles.Clear();
                pendingAvatarBans.Clear();
                currentAvatarByName.Clear();
                lastReportedAvatarIdByName.Clear();
                lastJoinLogAt.Clear();
                lastLeftLogAt.Clear();
                lastGroupCheckAt.Clear();
                reportedRipSuspicionMarkers.Clear();
                reportedUdonDetections.Clear();
                recentUdonExceptions.Clear();
                recentUdonInteractions.Clear();
                recentPickupEventsByUser.Clear();
                reportedPickupBurstUsers.Clear();
                lastUnattributedPickupWarningUtc = DateTime.MinValue;
                reportedForceTeleports.Clear();
                lastBanListActorName = string.Empty;
                lastBanListActorAtUtc = DateTime.MinValue;
                flaggedProfileUsers.Clear();
            }

            EmitLobbyPlayersSnapshot();

            foreach (var player in playersToLogLeft)
            {
                if (string.Equals(player.DisplayName, "Unknown", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ShouldLogUserEvent(player.UserId, false))
                    OnLog?.Invoke($"{player.DisplayName} left");
            }

            instancePollingCts?.Cancel();
            instancePollingCts?.Dispose();
            instancePollingCts = new System.Threading.CancellationTokenSource();

            var worldName = await ResolveWorldNameAsync(worldId);

            // Resolving the world name is a network call and multiple instance
            // switches can overlap. If a newer switch superseded this one while we
            // were awaiting, bail out so we don't overwrite the UI/log with the
            // previous world's name (stale resolve finishing last).
            if (!string.Equals(lastLoggedInstanceSignature, incomingSignature, StringComparison.OrdinalIgnoreCase))
                return;

            var worldDisplay = string.IsNullOrWhiteSpace(worldName) ? worldId : worldName;
            var instanceMessagePrefix = isStartupAttach ? "Current world detected" : "Rejoined world";
            OnLog?.Invoke($"[INSTANCE] {instanceMessagePrefix}: {worldDisplay}");
            OnWorldChanged?.Invoke(worldDisplay);

            var existingPlayers = detector.GetCurrentInstancePlayers(worldId, instanceId);
            foreach (var player in existingPlayers)
                await HandleObservedUserAsync(player.DisplayName, player.UserId);

            if (existingPlayers.Count > 0)
                OnLog?.Invoke($"[INFO] Detected {existingPlayers.Count} player(s) already in the current lobby.");

            ScanForRestrictedAreas();
            ScanForWorldUdonObjectsOnLoad();
            _ = ReportRestrictedAreaSummaryAsync();
            _ = StartInstancePollingLoopAsync(worldId, instanceId, instancePollingCts.Token);
        }

        private static bool TryParseCurrentLocation(string? location, out string worldId, out string instanceId)
        {
            worldId = string.Empty;
            instanceId = string.Empty;

            if (string.IsNullOrWhiteSpace(location))
                return false;

            if (!location.Contains("wrld_", StringComparison.OrdinalIgnoreCase))
                return false;

            var separatorIndex = location.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= location.Length - 1)
                return false;

            worldId = location.Substring(0, separatorIndex).Trim();
            instanceId = location.Substring(separatorIndex + 1).Trim().TrimEnd(',', '.', ';');

            if (!worldId.StartsWith("wrld_", StringComparison.OrdinalIgnoreCase))
                return false;

            return !string.IsNullOrWhiteSpace(instanceId);
        }

        private static string BuildInstanceSignature(string worldId, string instanceId)
        {
            var normalizedWorld = worldId?.Trim() ?? string.Empty;
            var normalizedInstance = instanceId?.Trim() ?? string.Empty;

            // VRChat may emit the same instance with additional qualifiers (~region/~nonce).
            // Use the base token for transition dedupe so one real transition logs once.
            var qualifierIndex = normalizedInstance.IndexOf('~');
            if (qualifierIndex > 0)
                normalizedInstance = normalizedInstance.Substring(0, qualifierIndex);

            return $"{normalizedWorld}:{normalizedInstance}";
        }

        private async Task StartInstancePollingLoopAsync(string worldId, string instanceId, System.Threading.CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (isStopped)
                        return;

                    // Stop if detector has moved to a newer instance.
                    if (!string.Equals(lastLoggedWorldId, worldId, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(lastLoggedInstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    await PollInstance(worldId, instanceId);
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Expected when instance changes.
            }
        }

        public async Task PollInstance(string worldId, string instanceId)
        {
            if (isStopped)
                return;

            try
            {
                JObject instance = await api.GetInstanceAsync(worldId, instanceId);

                if (instance["error"] != null)
                {
                    var message = instance["message"]?.ToString() ?? "Unknown error";
                    if (message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                        message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                    {
                        OnLog?.Invoke("[ERROR] Failed to fetch instance: access denied (403). Please click Login and complete session authentication/2FA, then try Start again.");
                    }
                    else
                    {
                        OnLog?.Invoke("[ERROR] Failed to fetch instance. Rejoin instance to fetch updated logs.");
                    }
                    return;
                }

                if (!IsConfiguredGroupInstance(instance))
                    return;

                JArray? users = await ExtractUsersAsync(instance);
                if (users == null)
                {
                    if (!usingLogUserFallback)
                    {
                        usingLogUserFallback = true;
                        OnLog?.Invoke("[INFO] Instance API payload has no user list for this world. Using VRChat log join lines for user detection.");
                    }
                    return;
                }

                HashSet<string> currentPlayers = new HashSet<string>();

                foreach (var user in users)
                {
                    string? userId = user["id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(userId))
                        continue;

                    currentPlayers.Add(userId);
                    string name = user["displayName"]?.ToString() ?? "Unknown";
                    lock (playerStateLock)
                    {
                        userIdToDisplayName[userId] = name;
                    }
                    _ = HandleObservedUserAsync(name, userId);
                }

                // LEAVE detection
                string[] previousPlayers;
                lock (playerStateLock)
                {
                    previousPlayers = knownPlayers.ToArray();
                }

                foreach (string oldUser in previousPlayers)
                {
                    if (string.Equals(oldUser, authenticatedUserId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!currentPlayers.Contains(oldUser))
                    {
                        string displayName;
                        lock (playerStateLock)
                        {
                            displayName = userIdToDisplayName.TryGetValue(oldUser, out var name) ? name : "Unknown";
                        }
                        if (string.Equals(displayName, "Unknown", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (ShouldLogUserEvent(oldUser, false))
                            OnLog?.Invoke($"{displayName} left");
                    }
                }

                lock (playerStateLock)
                {
                    knownPlayers.Clear();
                    foreach (string u in currentPlayers)
                        knownPlayers.Add(u);
                }

                EmitLobbyPlayersSnapshot();
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[ERROR] {ex.Message}");
            }
        }

        private bool IsConfiguredGroupInstance(JObject instance)
        {
            if (string.IsNullOrWhiteSpace(banTargetGroupId))
                return true;

            var hasGroupMetadata = instance.Property("groupId", StringComparison.OrdinalIgnoreCase) != null ||
                instance["group"]? ["id"] != null;
            if (!hasGroupMetadata)
                return true;

            var currentGroupId = instance["groupId"]?.ToString() ?? instance["group"]?["id"]?.ToString() ?? string.Empty;
            if (string.Equals(currentGroupId, banTargetGroupId, StringComparison.OrdinalIgnoreCase))
                return true;

            var location = string.IsNullOrWhiteSpace(currentGroupId) ? "a non-group instance" : $"group {currentGroupId}";
            var reason = $"Logging stopped because the current instance belongs to {location}, not the configured group.";
            OnLog?.Invoke($"[GROUP-INSTANCE] {reason}");
            OnStatus?.Invoke(reason);
            StopInternal();
            OnForcedStop?.Invoke(reason, 0);
            return false;
        }

        private async Task<JArray?> ExtractUsersAsync(JObject instance)
        {
            if (instance["users"] is JArray usersArray)
                return usersArray;

            // Fallback schemas observed across different VRChat instance endpoints.
            var idArrayKeys = new[] { "playerIds", "userIds", "memberIds", "players" };
            foreach (var key in idArrayKeys)
            {
                if (instance[key] is not JArray ids || ids.Count == 0)
                    continue;

                var synthesizedUsers = new JArray();

                foreach (var token in ids)
                {
                    string? userId = null;
                    string displayName = "Unknown";

                    if (token is JObject objToken)
                    {
                        userId = objToken["id"]?.ToString();
                        displayName = objToken["displayName"]?.ToString() ?? displayName;
                    }
                    else
                    {
                        userId = token?.ToString();
                    }

                    if (string.IsNullOrWhiteSpace(userId))
                        continue;

                    if (string.Equals(displayName, "Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        var user = await api.GetUserAsync(userId);
                        if (user["error"] == null)
                            displayName = user["displayName"]?.ToString() ?? displayName;
                    }

                    synthesizedUsers.Add(new JObject
                    {
                        ["id"] = userId,
                        ["displayName"] = displayName
                    });
                }

                return synthesizedUsers;
            }

            return null;
        }

        private void HandlePlayerLeft(string displayName, string userId)
        {
            if (isStopped)
                return;

            if (string.Equals(userId, authenticatedUserId, StringComparison.OrdinalIgnoreCase))
                return;

            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;

            // Cancel any pending group checks for this user
            System.Threading.CancellationTokenSource? cts = null;
            lock (playerStateLock)
            {
                if (playerCancellationTokens.TryGetValue(userId, out var found))
                {
                    cts = found;
                    playerCancellationTokens.Remove(userId);
                }
            }

            cts?.Cancel();
            cts?.Dispose();

            var shouldLogLeft = false;
            lock (playerStateLock)
            {
                var wasTracked = knownPlayers.Remove(userId);
                userIdToDisplayName.Remove(userId);
                runtimeStaffUserIds.Remove(userId);
                runtimeStaffGroupNames.Remove(userId);
                currentAvatarByName.Remove(safeName);
                lastReportedAvatarIdByName.Remove(safeName);
                pendingAvatarBans.Remove(safeName);
                shouldLogLeft = wasTracked && ShouldLogUserEvent(userId, false);
            }

            EmitLobbyPlayersSnapshot();

            if (shouldLogLeft && !string.Equals(safeName, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                OnLog?.Invoke($"{safeName} left");
                OnPlayerLeft?.Invoke(safeName, userId);
            }
        }

        private void ScanForRestrictedAreas()
        {
            // Clear reported markers when instance changes
            reportedRestrictedMarkers.Clear();
            // Load markers from config
            dynamicallyDetectedRestrictedAreas.Clear();
            foreach (var marker in restrictedAreaMarkers)
            {
                dynamicallyDetectedRestrictedAreas.Add(marker);
            }
        }

        private void CheckForRestrictedAreaInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Check world-specific configurations first
            if (worldRestrictedAreas.ContainsKey(lastLoggedWorldId ?? ""))
            {
                var worldConfigs = worldRestrictedAreas[lastLoggedWorldId ?? ""];
                foreach (var config in worldConfigs)
                {
                    bool detected = false;
                    string detectionMethod = "";

                    switch (config.DetectionMethod)
                    {
                        case "text":
                            if (!string.IsNullOrWhiteSpace(config.Pattern) && line.Contains(config.Pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                detected = true;
                                detectionMethod = "text";
                            }
                            break;
                        case "udon":
                            if (!string.IsNullOrWhiteSpace(config.Pattern) && line.Contains(config.Pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                detected = true;
                                detectionMethod = "Udon event";
                            }
                            break;
                        case "coords":
                            if (!string.IsNullOrWhiteSpace(config.Pattern) && line.Contains(config.Pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                detected = true;
                                detectionMethod = "coordinates";
                            }
                            break;
                    }

                    if (detected)
                    {
                        var markerKey = $"{config.DetectionMethod}:{config.Pattern}";
                        if (!reportedRestrictedMarkers.Contains(markerKey))
                        {
                            reportedRestrictedMarkers.Add(markerKey);
                            OnLog?.Invoke($"[RESTRICTED-AREA] Detected restricted area via {detectionMethod}: {config.Description}");
                            OnRestrictedAreaDetected?.Invoke($"Detected via {detectionMethod}: {config.Description}");

                            if (reportedRestrictedMarkers.Count == 1)
                            {
                                OnLog?.Invoke($"[RESTRICTED-AREA] This world has restricted areas. Locking access to group staff and owner only.");
                            }
                        }
                    }
                }
            }

            // Fallback to global markers
            foreach (var marker in dynamicallyDetectedRestrictedAreas)
            {
                if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    if (!reportedRestrictedMarkers.Contains(marker))
                    {
                        reportedRestrictedMarkers.Add(marker);
                        OnLog?.Invoke($"[RESTRICTED-AREA] Detected restricted area marker (global): '{marker}'");
                        OnRestrictedAreaDetected?.Invoke($"Detected restricted-area marker: '{marker}'");

                        if (reportedRestrictedMarkers.Count == 1)
                        {
                            OnLog?.Invoke($"[RESTRICTED-AREA] This world has restricted areas. Locking access to group staff and owner only.");
                        }
                    }
                }
            }
        }

        private void CheckForRipSuspicionInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || ripSuspicionMarkers.Count == 0)
                return;

            foreach (var marker in ripSuspicionMarkers)
            {
                if (!line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    continue;

                var userMatch = Regex.Match(line, @"(usr_[A-Za-z0-9\-]+)");
                var userId = userMatch.Success ? userMatch.Groups[1].Value : string.Empty;
                var key = string.IsNullOrWhiteSpace(userId)
                    ? $"line:{marker}:{line.GetHashCode()}"
                    : $"user:{userId}:{marker}";

                if (reportedRipSuspicionMarkers.Contains(key))
                    continue;

                reportedRipSuspicionMarkers.Add(key);

                if (!string.IsNullOrWhiteSpace(userId) &&
                    userIdToDisplayName.TryGetValue(userId, out var displayName) &&
                    !string.IsNullOrWhiteSpace(displayName))
                {
                    OnLog?.Invoke($"[RIP-SUS] Marker '{marker}' was found on {displayName} ({userId}). Action: manual review only.");
                    OnRipSuspicionDetected?.Invoke(displayName, userId, marker);
                }
                else if (!string.IsNullOrWhiteSpace(userId))
                {
                    OnLog?.Invoke($"[RIP-SUS] Marker '{marker}' was found on user {userId}. Action: manual review only.");
                    OnRipSuspicionDetected?.Invoke(string.Empty, userId, marker);
                }
                else
                {
                    OnLog?.Invoke($"[RIP-SUS] Marker '{marker}' detected in logs. Action: manual review only.");
                    OnRipSuspicionDetected?.Invoke(string.Empty, string.Empty, marker);
                }
            }
        }

        // Detects actual Udon button/UI-trigger patterns in the VRChat output log and reports
        // them as button-press detections even when the log does not include an explicit user id.
        private void CheckForWorldButtonPressesInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var matchedMarker = udonButtonPressMarkers
                .Where(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                .OrderBy(marker => marker.Length)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(matchedMarker) &&
                !Regex.IsMatch(line, @"(?i)(onbutton|button\s*(?:pressed|click(?:ed)?|trigger(?:ed)?|interaction)|toggle\s+(?:menu|video|drunk|fireworks|stage|particles|reflections|seats|post\s+processing|table\s+colliders|furniture\s+colliders)|verify\s+user|do\s+not\s+serve|prevent\s+pickup|jail\s+user|lockout\s+user|dumpster\s+player|call\s+security)", RegexOptions.CultureInvariant))
            {
                return;
            }

            var detectedSource = ResolveSourceIdentity(line);
            var userId = detectedSource.UserId;
            var displayName = detectedSource.DisplayName;
            var who = !string.IsNullOrWhiteSpace(displayName)
                ? (!string.IsNullOrWhiteSpace(userId) ? $"{displayName} ({userId})" : $"Player[{displayName}]")
                : (!string.IsNullOrWhiteSpace(userId) ? userId : "an unidentified source");

            var key = string.IsNullOrWhiteSpace(userId) ? $"button:{matchedMarker ?? "unknown"}:{line.Trim()}" : $"button:{userId}:{matchedMarker ?? "unknown"}";
            lock (playerStateLock)
            {
                if (!reportedButtonPressDetections.Add(key))
                    return;
            }

            var label = string.IsNullOrWhiteSpace(matchedMarker) ? "Udon button activity" : matchedMarker;
            OnLog?.Invoke($"[UDON-BUTTON] Udon button press detected: '{label}' from {who}.");
        }

        // Detects Udon activity in the logs and distinguishes generic interaction events
        // from malicious/crasher signatures. Generic Udon button/pickup activity is logged
        // to the Udon tab without auto-banning; only exploit markers or heavy exception
        // floods trigger a ban path.
        private void CheckForPickupBurstInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                !Regex.IsMatch(line, @"(?i)\b(picked\s*up|pickup|item\s*grab(?:bed)?|grabbed\s+item|onpickup)\b", RegexOptions.CultureInvariant))
            {
                return;
            }

            var userIdMatch = Regex.Match(line, @"\b(usr_[A-Za-z0-9\-]+)\b", RegexOptions.CultureInvariant);
            var detectedSource = ResolveSourceIdentity(line);
            var userId = userIdMatch.Success ? userIdMatch.Groups[1].Value : detectedSource.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                var now = DateTime.UtcNow;
                lock (playerStateLock)
                {
                    if (now - lastUnattributedPickupWarningUtc < pickupBurstWindow)
                        return;

                    lastUnattributedPickupWarningUtc = now;
                }

                OnLog?.Invoke("[PICKUP] Pickup/grab activity detected without a player ID. Logged for review; no automatic action taken.");
                return;
            }

            string displayName;
            int pickupCount;
            bool shouldReport;
            bool pickupFloodDetected;
            var isLocalUser = string.Equals(userId, authenticatedUserId, StringComparison.OrdinalIgnoreCase);
            lock (playerStateLock)
            {
                displayName = userIdToDisplayName.TryGetValue(userId, out var knownName) && !string.IsNullOrWhiteSpace(knownName)
                    ? knownName
                    : isLocalUser && !string.IsNullOrWhiteSpace(authenticatedDisplayName)
                        ? authenticatedDisplayName
                        : "Unknown";

                if (!recentPickupEventsByUser.TryGetValue(userId, out var events))
                {
                    events = new Queue<DateTime>();
                    recentPickupEventsByUser[userId] = events;
                }

                var now = DateTime.UtcNow;
                events.Enqueue(now);
                while (events.Count > 0 && now - events.Peek() > pickupBurstWindow)
                    events.Dequeue();

                pickupCount = events.Count;
                shouldReport = pickupCount == 1 || pickupCount % pickupLogBatchSize == 0 || pickupCount == pickupBurstThreshold;
                pickupFloodDetected = pickupCount >= pickupBurstThreshold && reportedPickupBurstUsers.Add(userId);
            }

            if (shouldReport)
                OnLog?.Invoke($"[PICKUP] Who: {displayName} ({userId}) | Items picked up: {pickupCount} in {pickupBurstWindow.TotalSeconds:0}s.");

            if (!pickupFloodDetected)
                return;

            var description = $"a possible scripted item-pickup/Udon flood ({pickupBurstThreshold}+ events in {pickupBurstWindow.TotalSeconds:0}s)";
            OnLog?.Invoke($"[PICKUP-FLOOD] {displayName} ({userId}) triggered {description}. Review required; no automatic ban was issued.");
            OnUdonExploitDetected?.Invoke(displayName, userId, description);
        }

        private void CheckForUdonExploitInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            string? description = null;
            bool shouldAutoBan = false;

            foreach (var marker in udonExploitMarkers)
            {
                if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    description = $"malicious Udon indicator '{marker}'";
                    shouldAutoBan = true;
                    break;
                }
            }

            if (description == null &&
                line.Contains(udonExceptionSignature, StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTime.UtcNow;
                lock (playerStateLock)
                {
                    recentUdonExceptions.Add(now);
                    recentUdonExceptions.RemoveAll(t => now - t > udonFloodWindow);
                    if (recentUdonExceptions.Count >= udonFloodThreshold)
                    {
                        description = $"a massive Udon exception flood ({recentUdonExceptions.Count} in {udonFloodWindow.TotalSeconds:0}s)";
                        recentUdonExceptions.Clear();
                        shouldAutoBan = true;
                    }
                }
            }

            if (description == null)
            {
                var isLikelyButtonAction =
                    (line.Contains("button", StringComparison.OrdinalIgnoreCase) &&
                     (line.Contains("press", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("pressed", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("click", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("clicked", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("trigger", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("toggle", StringComparison.OrdinalIgnoreCase))) ||
                    line.Contains("toggle fireworks show", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("call security", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle drum", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("show message", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("fireworks show", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("message panel", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle post processing", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle reflections", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle particles", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle table colliders", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle furniture colliders", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle seats", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("override dynamic speed", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle video player", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle drunk audio", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("activate voice ducking", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("show voice ducking area of effect", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("activate microphone", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle stage dj deck", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("do not serve", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("prevent pickup", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle menu locks", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle menu gravity", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("toggle log", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("verify user", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("dumpster player", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("lockout user", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("jail user", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("security panel", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("management panel", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("assistant manager", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("fallback gen. manager", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("give gen. manager", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("security", StringComparison.OrdinalIgnoreCase) && line.Contains("panel", StringComparison.OrdinalIgnoreCase);

                if (isLikelyButtonAction)
                {
                    description = "Udon runtime activity 'button action'";
                }
            }

            if (description == null)
            {
                foreach (var marker in udonActivityMarkers)
                {
                    if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        description = $"Udon runtime activity '{marker}'";
                        break;
                    }
                }
            }

            if (description == null)
                return;

            var interactionDetected = description.StartsWith("Udon runtime activity", StringComparison.OrdinalIgnoreCase);
            if (interactionDetected)
            {
                var now = DateTime.UtcNow;
                lock (playerStateLock)
                {
                    recentUdonInteractions.Add(now);
                    recentUdonInteractions.RemoveAll(t => now - t > udonFloodWindow);
                    if (recentUdonInteractions.Count >= udonInteractionThreshold)
                    {
                        description = $"a burst of Udon interaction activity ({recentUdonInteractions.Count} events in {udonFloodWindow.TotalSeconds:0}s)";
                        recentUdonInteractions.Clear();
                    }
                }
            }

            var detectedSource = ResolveSourceIdentity(line);
            var userId = detectedSource.UserId;

            var key = string.IsNullOrWhiteSpace(userId) ? $"udon:line:{description}" : $"udon:user:{userId}";
            lock (playerStateLock)
            {
                if (!reportedUdonDetections.Add(key))
                    return;
            }

            var displayName = detectedSource.DisplayName;

            var who = !string.IsNullOrWhiteSpace(displayName)
                ? (!string.IsNullOrWhiteSpace(userId) ? $"{displayName} ({userId})" : (string.IsNullOrWhiteSpace(displayName) ? "an unidentified source" : $"Player[{displayName}]"))
                : (!string.IsNullOrWhiteSpace(userId) ? userId : "an unidentified source");

            OnLog?.Invoke($"[UDON-DETECT] Detected {description} from {who}.");

            if (shouldAutoBan)
            {
                OnUdonExploitDetected?.Invoke(displayName, userId, description);
                if (!string.IsNullOrWhiteSpace(userId))
                    _ = TryAutoBanAsync(string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName, userId, "triggering a massive Udon detection");
            }
        }

        private void ScanForWorldUdonObjectsOnLoad()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(detector.LogFolder))
                    return;

                var latestLog = Directory.GetFiles(detector.LogFolder, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(file => Path.GetFileName(file).Contains("output_log", StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileName(file).Contains("vrchat", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(file => File.GetLastWriteTime(file))
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(latestLog) || !File.Exists(latestLog))
                    return;

                var recentLines = File.ReadLines(latestLog)
                    .Skip(Math.Max(0, File.ReadLines(latestLog).Count() - 400))
                    .ToList();

                var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in recentLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    foreach (var marker in udonWorldObjectMarkers)
                    {
                        if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(marker);
                            break;
                        }
                    }
                }

                if (matches.Count == 0)
                    return;

                var summary = string.Join(", ", matches.OrderBy(x => x).Take(8));
                OnLog?.Invoke($"[WORLD-UDON] Potential Udon objects detected on world load: {summary}.");
            }
            catch
            {
                // Best-effort scan only.
            }
        }

        // Detects Modern UI panels in the instance and staff-owner ownership chains. This
        // is used to flag suspicious world-panel behavior and optionally auto-ban the
        // player who spawns or owns the panel when ownership matches a staff/moderator.
        private void CheckForModernUiPanelInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            bool isModernUi = line.Contains("ModernUI", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Modern UI", StringComparison.OrdinalIgnoreCase)
                || line.Contains("ModernUi", StringComparison.OrdinalIgnoreCase)
                || line.Contains("SimpleUI", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Simple UI", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Hash Studios Admin Ban Menu", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Admin Ban Menu", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Hash Studios", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Reimajo", StringComparison.OrdinalIgnoreCase)
                || line.Contains("reimajo admin panel", StringComparison.OrdinalIgnoreCase)
                || line.Contains("reimajo panel", StringComparison.OrdinalIgnoreCase)
                || line.Contains("panel owner", StringComparison.OrdinalIgnoreCase)
                || line.Contains("panel ownership", StringComparison.OrdinalIgnoreCase)
                || line.Contains("staff-owned panel", StringComparison.OrdinalIgnoreCase)
                || line.Contains("owner of the panel", StringComparison.OrdinalIgnoreCase)
                || (line.Contains("panel", StringComparison.OrdinalIgnoreCase) &&
                    (line.Contains("owner", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("moderator", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("instance owner", StringComparison.OrdinalIgnoreCase)));

            if (!isModernUi)
                return;

            var detectedSource = ResolveSourceIdentity(line);
            var userId = detectedSource.UserId;

            string displayName = detectedSource.DisplayName;

            var ownershipPhrase = line.Contains("owner", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("owned", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("instance owner", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("moderator", StringComparison.OrdinalIgnoreCase)
                ? "panel ownership detected"
                : "Modern UI panel detected";

            var sourceKey = string.IsNullOrWhiteSpace(userId)
                ? (string.IsNullOrWhiteSpace(displayName) ? "unknown" : displayName.Trim())
                : userId;
            var key = $"modernui:{lastLoggedInstanceSignature}:{sourceKey}:{ownershipPhrase}";
            lock (playerStateLock)
            {
                if (!reportedModernUiDetections.Add(key))
                    return;
            }

            var who = !string.IsNullOrWhiteSpace(displayName)
                ? (!string.IsNullOrWhiteSpace(userId) ? $"{displayName} ({userId})" : (string.IsNullOrWhiteSpace(displayName) ? "an unidentified source" : $"Player[{displayName}]"))
                : (!string.IsNullOrWhiteSpace(userId) ? userId : "an unidentified source");

            OnLog?.Invoke($"[WORLD-MOD] {ownershipPhrase} in the instance from {who}.");
            OnModernUiPanelDetected?.Invoke(displayName, userId, ownershipPhrase);

            if (!string.IsNullOrWhiteSpace(userId) &&
                (line.Contains("owner", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("moderator", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("instance owner", StringComparison.OrdinalIgnoreCase)))
            {
                _ = TryAutoBanAsync(displayName, userId, "owning or spawning a staff-owned Modern UI panel");
            }
        }

        // Detects being force-teleported to a world's ban/out-of-bounds zone and bans the
        // player responsible. World ban prefabs (e.g. "ModernUIs") log the moderator
        // updating the banned-players list right before the local player is teleported;
        // that correlation is what attributes the teleport to a specific player.
        private void CheckForForceTeleportInLogs(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Remember who last updated the world's banned-players list.
            var actorMatch = banListActorRegex.Match(line);
            if (actorMatch.Success)
            {
                var name = CleanDecoratedName(actorMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    lock (playerStateLock)
                    {
                        lastBanListActorName = name;
                        lastBanListActorAtUtc = DateTime.UtcNow;
                    }
                }
                return;
            }

            // The local player being force-teleported by a world zone check.
            if (!line.Contains("breaching the zone check", StringComparison.OrdinalIgnoreCase))
                return;

            string actorName;
            DateTime actorAt;
            lock (playerStateLock)
            {
                actorName = lastBanListActorName;
                actorAt = lastBanListActorAtUtc;
            }

            // Without a recent ban-list update by a named player, this is a normal world
            // zone teleport (portal), not a malicious force-teleport. Ignore it silently.
            if (string.IsNullOrWhiteSpace(actorName) || DateTime.UtcNow - actorAt > forceTeleportWindow)
                return;

            var userId = ResolveDecoratedActorUserId(actorName);

            lock (playerStateLock)
            {
                var key = string.IsNullOrWhiteSpace(userId) ? $"tp:name:{actorName}" : $"tp:user:{userId}";
                if (!reportedForceTeleports.Add(key))
                    return;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                OnLog?.Invoke($"[FORCE-TP] {actorName} force-teleported you to the ban area, but their user id is not known yet \u2014 cannot ban.");
                OnForceTeleportDetected?.Invoke(actorName, string.Empty);
                return;
            }

            OnLog?.Invoke($"[FORCE-TP] {actorName} ({userId}) force-teleported you to the ban area. Banning.");
            OnForceTeleportDetected?.Invoke(actorName, userId);
            _ = TryAutoBanAsync(actorName, userId, "force-teleporting a member to a ban/out-of-bounds area");
        }

        // World name-display prefabs decorate names (e.g. a leading "-" or rank symbols).
        // Strips non-alphanumeric decoration from the ends so the real name can be matched.
        private static string CleanDecoratedName(string name)
        {
            var s = (name ?? string.Empty).Trim();
            int start = 0;
            while (start < s.Length && !char.IsLetterOrDigit(s[start]))
                start++;
            int end = s.Length - 1;
            while (end >= start && !char.IsLetterOrDigit(s[end]))
                end--;
            return start <= end ? s.Substring(start, end - start + 1).Trim() : s.Trim();
        }

        private (string DisplayName, string UserId) ResolveSourceIdentity(string line, string? fallbackUserId = null)
        {
            if (!string.IsNullOrWhiteSpace(authenticatedDisplayName) &&
                line.Contains("pickup object", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("last input method", StringComparison.OrdinalIgnoreCase))
            {
                return (authenticatedDisplayName, authenticatedUserId ?? string.Empty);
            }

            var userId = !string.IsNullOrWhiteSpace(fallbackUserId)
                ? fallbackUserId
                : Regex.Match(line, @"(usr_[A-Za-z0-9\-]+)").Success
                    ? Regex.Match(line, @"(usr_[A-Za-z0-9\-]+)").Groups[1].Value
                    : string.Empty;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                lock (playerStateLock)
                {
                    if (userIdToDisplayName.TryGetValue(userId, out var knownName) &&
                        !string.IsNullOrWhiteSpace(knownName))
                    {
                        return (knownName, userId);
                    }
                }
            }

            lock (playerStateLock)
            {
                foreach (var kvp in userIdToDisplayName)
                {
                    var trackedName = kvp.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(trackedName))
                        continue;

                    if (line.Contains(trackedName, StringComparison.OrdinalIgnoreCase) ||
                        line.Contains(CleanDecoratedName(trackedName), StringComparison.OrdinalIgnoreCase))
                    {
                        return (trackedName, kvp.Key);
                    }
                }
            }

            var namePatterns = new[]
            {
                @"(?:Player|User|Actor|Source|Owner|Moderator|Staff)\s*\[\s*([^\]]{2,64})\s*\]",
                @"(?:player|user|actor|source|owner|moderator|staff)\s*\[\s*([^\]]{2,64})\s*\]",
                @"(?:picked up by|pickup by|item from|interacted by|from|by)\s+(?:Player\s*\[)?([^\]\r\n]{2,64})(?:\])?",
                @"(?:picked up|pickup|item pickup|interaction)[^A-Za-z0-9_-]{0,18}([A-Z][A-Za-z0-9'._ -]{2,32})",
                @"(?:player|user|actor|source|owner|moderator|staff)[^A-Za-z0-9_-]{0,12}([A-Z][A-Za-z0-9'._ -]{2,32})"
            };

            foreach (var pattern in namePatterns)
            {
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                var candidateName = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(candidateName))
                    continue;

                candidateName = candidateName.Trim('"', '\'', '[', ']', '(', ')', '{', '}');
                if (string.IsNullOrWhiteSpace(candidateName))
                    continue;

                var exactId = FindUserIdByDisplayName(candidateName);
                if (!string.IsNullOrWhiteSpace(exactId))
                    return (candidateName, exactId);

                lock (playerStateLock)
                {
                    foreach (var kvp in userIdToDisplayName)
                    {
                        var trackedName = kvp.Value ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(trackedName))
                            continue;

                        if (string.Equals(CleanDecoratedName(trackedName), CleanDecoratedName(candidateName), StringComparison.OrdinalIgnoreCase) ||
                            candidateName.Contains(trackedName, StringComparison.OrdinalIgnoreCase) ||
                            trackedName.Contains(candidateName, StringComparison.OrdinalIgnoreCase))
                        {
                            return (trackedName, kvp.Key);
                        }
                    }
                }
            }

            var titleCaseFallback = Regex.Match(line, @"([A-Z][a-z]+(?:\s+[A-Z][a-z]+){0,2})");
            if (titleCaseFallback.Success)
            {
                var fallbackName = titleCaseFallback.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(fallbackName))
                {
                    var exactId = FindUserIdByDisplayName(fallbackName);
                    if (!string.IsNullOrWhiteSpace(exactId))
                        return (fallbackName, exactId);
                }
            }

            lock (playerStateLock)
            {
                if (userIdToDisplayName.Count > 0)
                {
                    var mostRecentKnownPlayer = userIdToDisplayName.Last();
                    if (!string.IsNullOrWhiteSpace(mostRecentKnownPlayer.Value))
                        return (mostRecentKnownPlayer.Value, mostRecentKnownPlayer.Key);
                }
            }

            if (!string.IsNullOrWhiteSpace(authenticatedDisplayName))
            {
                return (authenticatedDisplayName, authenticatedUserId ?? string.Empty);
            }

            return (string.Empty, userId);
        }

        // Resolves a decorated ban-list actor name to a tracked user id, tolerating the
        // decoration world prefabs add. Falls back to a contains match on the real names.
        private string? ResolveDecoratedActorUserId(string decoratedName)
        {
            if (string.IsNullOrWhiteSpace(decoratedName))
                return null;

            var cleaned = CleanDecoratedName(decoratedName);

            var exact = FindUserIdByDisplayName(cleaned);
            if (!string.IsNullOrWhiteSpace(exact))
                return exact;

            lock (playerStateLock)
            {
                foreach (var kvp in userIdToDisplayName)
                {
                    var tracked = kvp.Value;
                    if (string.IsNullOrWhiteSpace(tracked))
                        continue;

                    if (string.Equals(CleanDecoratedName(tracked), cleaned, StringComparison.OrdinalIgnoreCase) ||
                        cleaned.Contains(tracked, StringComparison.OrdinalIgnoreCase) ||
                        decoratedName.Contains(tracked, StringComparison.OrdinalIgnoreCase))
                        return kvp.Key;
                }
            }

            return null;
        }

        private void DebugLogScan(string line)
        {
            // Debug: show all log lines being scanned (limited to first 80 chars to avoid spam)
            if (line.Length > 0)
            {
                OnLog?.Invoke($"[DEBUG-SCAN] {line.Substring(0, Math.Min(80, line.Length))}");
            }
        }

        private async Task ReportRestrictedAreaSummaryAsync()
        {
            // Wait a moment to allow log scanning to catch markers
            await Task.Delay(2000);

            if (reportedRestrictedMarkers.Count > 0)
            {
                OnLog?.Invoke($"[RESTRICTED-AREA] Found {reportedRestrictedMarkers.Count} restricted area(s). Access locked to group staff and owner only.");
            }
        }

        private async Task HandleObservedUserAsync(string displayName, string userId)
        {
            if (isStopped)
                return;

            if (string.IsNullOrWhiteSpace(userId))
                return;

            if (string.Equals(userId, authenticatedUserId, StringComparison.OrdinalIgnoreCase))
                return;

            var shouldProcess = false;
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;
            lock (playerStateLock)
            {
                if (!knownPlayers.Contains(userId))
                {
                    knownPlayers.Add(userId);
                    userIdToDisplayName[userId] = safeName;

                    var now = DateTime.UtcNow;
                    if (!(lastGroupCheckAt.TryGetValue(userId, out var lastCheckedAt) && now - lastCheckedAt < duplicateGroupCheckWindow))
                    {
                        lastGroupCheckAt[userId] = now;
                        shouldProcess = true;
                    }
                }
            }

            if (!shouldProcess)
                return;

            EmitLobbyPlayersSnapshot();

            // A blacklisted avatar may have been seen before this player's id was known.
            string? pendingAvatar;
            lock (playerStateLock)
            {
                pendingAvatarBans.TryGetValue(safeName, out pendingAvatar);
                if (pendingAvatar != null)
                    pendingAvatarBans.Remove(safeName);
            }
            if (!string.IsNullOrWhiteSpace(pendingAvatar))
            {
                bool alreadyProcessed;
                lock (playerStateLock)
                {
                    alreadyProcessed = !processedAvatarBanUsers.Add(userId);
                }
                if (!alreadyProcessed)
                {
                    var avatarReason = $"wearing blacklisted avatar '{pendingAvatar}'";
                    OnBlacklistedAvatarMatched?.Invoke(safeName, userId, pendingAvatar);
                    OnLog?.Invoke($"[AVATAR] {safeName} matched blacklisted avatar '{pendingAvatar}'.");
                    await TryAutoBanAsync(safeName, userId, avatarReason);
                    return;
                }
            }

            // During immediate rejoin transition, force visible order as:
            // left -> joined -> group-check for returning players.
            if (DateTime.UtcNow <= rejoinTransitionUntilUtc &&
                !string.Equals(safeName, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                ShouldLogUserEvent(userId, false))
            {
                OnLog?.Invoke($"{safeName} left");
            }

            if (ShouldLogUserEvent(userId, true))
            {
                OnLog?.Invoke($"Player joined: {safeName}");
                OnPlayerJoined?.Invoke(safeName, userId);
            }

            await playerProcessingSemaphore.WaitAsync();
            try
            {
                // If a cooldown started (or logging was stopped) while this player was
                // queued behind the semaphore, exit quietly instead of spamming the log.
                if (isStopped || api.IsCoolingDown)
                    return;

                // Create cancellation token for this player's checks
                var cts = new System.Threading.CancellationTokenSource();
                lock (playerStateLock)
                {
                    playerCancellationTokens[userId] = cts;
                }
                var token = cts.Token;

                OnLog?.Invoke($"[GROUP-CHECK] Checking for blacklisted groups on player: {safeName}");

                var matchedEntry = FindBlacklistMatch(safeName, userId);
                if (!string.IsNullOrWhiteSpace(matchedEntry) && IsIdEntry(matchedEntry))
                {
                    OnLog?.Invoke($"[GROUP-CHECK] Blacklist entry found for {safeName}. Banning from owned group.");
                    var reason = $"matching blacklist entry '{matchedEntry}'";
                    await TryAutoBanAsync(safeName, userId, reason);
                    return;
                }

                await CheckProfileFlagsAsync(safeName, userId, token);
                if (token.IsCancellationRequested)
                    return;

                if (ownedGroupIds.Count > 0)
                {
                    string? matchedOwnedGroupId = null;
                    foreach (var groupId in ownedGroupIds)
                    {
                        token.ThrowIfCancellationRequested();
                        var staffRole = await api.GetUserGroupStaffRoleAsync(groupId, userId);
                        if (staffRole != null)
                        {
                            matchedOwnedGroupId = groupId;
                            lock (playerStateLock)
                                runtimeStaffRoles[userId] = staffRole;
                            break;
                        }
                    }

                    if (matchedOwnedGroupId != null)
                    {
                        var staffGroupName = await api.GetGroupNameAsync(matchedOwnedGroupId) ?? matchedOwnedGroupId;
                        lock (playerStateLock)
                        {
                            runtimeStaffUserIds.Add(userId);
                            runtimeStaffGroupNames[userId] = staffGroupName;
                        }
                        OnLog?.Invoke($"[GROUP-CHECK] {safeName} is approved staff for owned group \"{staffGroupName}\".");
                        EmitLobbyPlayersSnapshot();
                        return;
                    }
                }

                if (configuredBlacklistedGroupIds.Count > 0)
                {
                    if (api.IsCoolingDown)
                    {
                        OnLog?.Invoke($"[GROUP-CHECK] Scan skipped for {safeName}: API cooldown active.");
                        return;
                    }

                    // Fetch the user's groups ONCE, then evaluate the whole blacklist
                    // locally. This keeps the group scan at a single API call per player
                    // instead of one call per blacklisted group.
                    var snapshotReady = await api.TryCacheUserGroupsAsync(userId, token);
                    if (token.IsCancellationRequested)
                        return;

                    if (!snapshotReady)
                    {
                        OnLog?.Invoke($"[GROUP-CHECK] Scan incomplete for {safeName}: could not load group data (API cooldown or private profile).");
                        return;
                    }

                    // Staff-group check reuses the cached snapshot (no extra API calls).
                    // Members of a configured staff group are exempt from bans and listed as staff.
                    if (staffGroupIds.Count > 0)
                    {
                        string? matchedStaffGroupId = null;
                        foreach (var groupId in staffGroupIds)
                        {
                            token.ThrowIfCancellationRequested();
                            if (await api.IsUserInGroupAsync(userId, groupId, token))
                            {
                                matchedStaffGroupId = groupId;
                                break;
                            }
                        }

                        if (token.IsCancellationRequested)
                            return;

                        if (matchedStaffGroupId != null)
                        {
                            bool added;
                            lock (playerStateLock)
                            {
                                added = runtimeStaffUserIds.Add(userId);
                            }
                            var staffGroupName = await api.GetGroupNameAsync(matchedStaffGroupId) ?? matchedStaffGroupId;
                            OnLog?.Invoke($"[GROUP-CHECK] {safeName} cannot be banned - they are a group staff member (staff group \"{staffGroupName}\").");
                            if (added)
                                EmitLobbyPlayersSnapshot();
                            return;
                        }
                    }

                    var matchedGroups = new List<string>();
                    foreach (var groupId in configuredBlacklistedGroupIds)
                    {
                        token.ThrowIfCancellationRequested();
                        if (await api.IsUserInGroupAsync(userId, groupId, token))
                            matchedGroups.Add(groupId);
                    }

                    if (token.IsCancellationRequested)
                        return;

                    if (matchedGroups.Count > 0)
                    {
                        var matchedGroupId = matchedGroups[0];
                        var matchedGroupName = await api.GetGroupNameAsync(matchedGroupId) ?? matchedGroupId;
                        var ownedGroupName = await api.GetGroupNameAsync(banTargetGroupId) ?? banTargetGroupId;
                        var reason = $"user was participating in the blacklisted group \"{matchedGroupName}\", which is not allowed in the {ownedGroupName}";
                        OnLog?.Invoke($"[GROUP-CHECK] Blacklisted group found: {matchedGroupName} was found on {safeName}.");
                        OnBlacklistedGroupMatched?.Invoke(safeName, matchedGroupId, matchedGroupName);
                        await TryAutoBanAsync(safeName, userId, reason);
                    }
                    else
                    {
                        OnLog?.Invoke($"[GROUP-CHECK] {safeName} is not participating in any blacklisted groups.");
                    }
                }
                else
                {
                    OnLog?.Invoke($"[GROUP-CHECK] No blacklisted groups configured. Ignore.");
                }

                await Task.CompletedTask;
            }
            catch (System.OperationCanceledException)
            {
                // Player left during check, silently exit
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[ERROR] Group check failed for {safeName}: {ex.Message}");
            }
            finally
            {
                playerProcessingSemaphore.Release();
                // Clean up cancellation token
                System.Threading.CancellationTokenSource? cts = null;
                lock (playerStateLock)
                {
                    if (playerCancellationTokens.TryGetValue(userId, out var found))
                    {
                        cts = found;
                        playerCancellationTokens.Remove(userId);
                    }
                }

                cts?.Dispose();
            }
        }

        private void EmitLobbyPlayersSnapshot()
        {
            List<string> players;
            List<LobbyPlayer> detailed;
            lock (playerStateLock)
            {
                detailed = knownPlayers
                    .Select(userId =>
                    {
                        var name = userIdToDisplayName.TryGetValue(userId, out var n) && !string.IsNullOrWhiteSpace(n)
                            ? n
                            : userId;
                        return (userId, name);
                    })
                    .Where(p => !string.IsNullOrWhiteSpace(p.name) && !string.Equals(p.name, "Unknown", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                    .Select(p =>
                    {
                        currentAvatarByName.TryGetValue(p.name, out var av);
                        return new LobbyPlayer
                        {
                            DisplayName = p.name,
                            UserId = p.userId,
                            AvatarName = av.Name,
                            AvatarId = av.Id,
                            IsStaff = IsLocalStaff(p.userId, p.name),
                            StaffGroupName = runtimeStaffGroupNames.TryGetValue(p.userId, out var staffGroupName)
                                ? staffGroupName
                                : null,
                            StaffRole = runtimeStaffRoles.TryGetValue(p.userId, out var staffRole)
                                ? staffRole
                                : null
                        };
                    })
                    .ToList();

                players = detailed
                    .Select(p => p.IsStaff ? $"{p.DisplayName}  \u2014  [GROUP STAFF]" : p.DisplayName)
                    .ToList();
            }

            OnLobbyPlayersChanged?.Invoke(players);
            OnLobbyPlayersDetailed?.Invoke(detailed);
        }

        private bool IsLocalStaff(string? userId, string? displayName)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                lock (playerStateLock)
                {
                    if (runtimeStaffUserIds.Contains(userId))
                        return true;
                }
                if (staffUserIds.Contains(userId))
                    return true;
            }
            if (!string.IsNullOrWhiteSpace(displayName) && staffDisplayNames.Contains(displayName))
                return true;
            return false;
        }

        private static (HashSet<string> ids, HashSet<string> names) LoadGroupStaff()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = RuntimePaths.GetFile("group_staff.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path,
@"# Group staff list. Anyone listed here is marked as [GROUP STAFF] in the
# lobby player list and is NEVER auto-banned.
#
# Add one entry per line. You can use either:
#   - The user's ID (starts with usr_)   e.g.  usr_12345678-aaaa-bbbb-cccc-1234567890ab
#   - The user's exact display name        e.g.  Example Username
#
# IDs are the most reliable since display names can change.
# NOTE: group membership IDs (gmem_) do NOT work here - use the usr_ ID or the name.
# Lines starting with # are ignored.
");
                return (ids, names);
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("usr_", StringComparison.OrdinalIgnoreCase))
                    ids.Add(line);
                else
                    names.Add(line);
            }

            return (ids, names);
        }

        private static HashSet<string> LoadStaffGroups()
        {
            var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = RuntimePaths.GetFile("staff_groups.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path,
@"# Staff groups. During a group scan, if a player belongs to ANY group listed
# here, they are marked as [GROUP STAFF] in the lobby and are NEVER auto-banned.
#
# Add one group ID per line (starts with grp_)   e.g.  grp_12345678-aaaa-bbbb-cccc-1234567890ab
#
# This reuses the group data already fetched during the scan, so it adds no
# extra API calls. Lines starting with # are ignored.
");
                return groupIds;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                    groupIds.Add(line);
            }

            return groupIds;
        }

        private static HashSet<string> LoadWhitelistedGroupIds()
        {
            var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = RuntimePaths.GetFile("group_whitelist.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path,
@"# Group whitelist. Members of any group listed here bypass all automatic ban moderations.
#
# Add one group ID per line (starts with grp_).
# Lines starting with # are ignored.
" );
                return groupIds;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw?.Trim();
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#") && line.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                    groupIds.Add(line);
            }

            return groupIds;
        }

        private async Task HandleRestrictedAreaLineAsync(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // If no markers are configured or detected, this detection is intentionally disabled.
            if (dynamicallyDetectedRestrictedAreas.Count == 0)
                return;

            var marker = dynamicallyDetectedRestrictedAreas.FirstOrDefault(m =>
                line.Contains(m, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(marker))
                return;

            var userMatch = Regex.Match(line, @"(usr_[A-Za-z0-9\-]+)");
            if (!userMatch.Success)
                return;

            var userId = userMatch.Groups[1].Value;
            var name = "Unknown";

            // Try to use the usual join-line name pattern if present.
            var joinPattern = Regex.Match(line, @"OnPlayerJoined\s+(.+?)\s+\((usr_[A-Za-z0-9\-]+)\)");
            if (joinPattern.Success)
                name = joinPattern.Groups[1].Value.Trim();

            if (string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                var user = await api.GetUserAsync(userId);
                if (user["error"] == null)
                    name = user["displayName"]?.ToString() ?? name;
            }

            var reason = $"restricted-area teleport/staff-zone detection marker '{marker}'";
            await TryAutoBanAsync(name, userId, reason);
        }

        // Reacts to an avatar a player switched into. If the avatar's name or id is on
        // the per-account avatar blacklist, the wearer is auto-banned from the group.
        private async Task HandleObservedAvatarAsync(string displayName, string? avatarName, string? avatarId)
        {
            if (isStopped)
                return;

            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName.Trim();
            string? observedAvatarName;
            string? observedAvatarId;

            // Record the player's current avatar first, so a later blacklist addition can
            // be checked against everyone already present (even when the list is empty now).
            lock (playerStateLock)
            {
                currentAvatarByName.TryGetValue(safeName, out var existing);
                var nextAvatarName = !string.IsNullOrWhiteSpace(avatarName)
                    ? avatarName.Trim()
                    : existing.Name;
                var nextAvatarId = !string.IsNullOrWhiteSpace(avatarId)
                    ? avatarId.Trim()
                    : existing.Id;

                // A brand-new switch must overwrite any stale avatar still associated with this
                // player, even if the old value is still cached in memory.
                if (!string.IsNullOrWhiteSpace(avatarName))
                    nextAvatarId = null;

                currentAvatarByName[safeName] = (nextAvatarName, nextAvatarId);
                observedAvatarName = nextAvatarName;
                observedAvatarId = nextAvatarId;
            }

            OnLog?.Invoke($"[AVATAR] {safeName} switched avatar to '{avatarName ?? avatarId ?? "Unknown"}'.");
            EmitLobbyPlayersSnapshot();

            if (!string.IsNullOrWhiteSpace(avatarName) || !string.IsNullOrWhiteSpace(avatarId))
                _ = EmitAvatarObservationAsync(safeName, observedAvatarName, observedAvatarId);

            if (configuredBlacklistedAvatars.Count == 0)
                return;

            // Never act on our own avatar.
            if (!string.IsNullOrWhiteSpace(authenticatedDisplayName) &&
                string.Equals(safeName, authenticatedDisplayName, StringComparison.OrdinalIgnoreCase))
                return;

            var matched = FindAvatarBlacklistMatch(avatarName, avatarId);
            if (matched == null)
                return;

            await BanAvatarWearerAsync(safeName, matched);
        }

        private async Task EmitAvatarObservationAsync(string displayName, string? observedAvatarName, string? observedAvatarId)
        {
            try
            {
                // Give VRChat a moment to emit a nearby avtr_ ID before falling back to
                // the avatar name recorded in the switch line.
                await Task.Delay(TimeSpan.FromSeconds(2));

                string avatarName;
                string avatarId;
                lock (playerStateLock)
                {
                    if (string.IsNullOrWhiteSpace(observedAvatarName))
                        return;

                    avatarName = observedAvatarName;
                    avatarId = observedAvatarId ?? string.Empty;

                    // A name line and its avtr_ ID can arrive separately. Merge the ID
                    // only when the player is still on the same avatar; otherwise retain
                    // the snapshot so rapid A -> B -> C changes each get reported.
                    if (currentAvatarByName.TryGetValue(displayName, out var currentAvatar) &&
                        string.Equals(currentAvatar.Name, avatarName, StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(avatarId))
                    {
                        avatarId = currentAvatar.Id ?? string.Empty;
                    }

                    var observationKey = !string.IsNullOrWhiteSpace(avatarId)
                        ? avatarId
                        : avatarName;
                    if (
                        (lastReportedAvatarIdByName.TryGetValue(displayName, out var previousAvatarId) &&
                         string.Equals(previousAvatarId, observationKey, StringComparison.OrdinalIgnoreCase)))
                        return;

                    lastReportedAvatarIdByName[displayName] = observationKey;
                }

                var wearerUserId = FindUserIdByDisplayName(displayName)
                    ?? (string.Equals(displayName, authenticatedDisplayName, StringComparison.OrdinalIgnoreCase)
                        ? authenticatedUserId
                        : null)
                    ?? string.Empty;
                string? thumbnailUrl = null;
                if (!string.IsNullOrWhiteSpace(wearerUserId))
                {
                    var user = await api.GetUserAsync(wearerUserId);
                    thumbnailUrl = user["currentAvatarImageUrl"]?.ToString() ?? user["currentAvatarThumbnailImageUrl"]?.ToString();
                    if (string.IsNullOrWhiteSpace(thumbnailUrl))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        user = await api.GetUserAsync(wearerUserId);
                        thumbnailUrl = user["currentAvatarImageUrl"]?.ToString() ?? user["currentAvatarThumbnailImageUrl"]?.ToString();
                    }
                }
                else if (avatarId.StartsWith("avtr_", StringComparison.OrdinalIgnoreCase))
                {
                    var avatarDetails = await api.GetAvatarAsync(avatarId);
                    thumbnailUrl = avatarDetails["imageUrl"]?.ToString() ?? avatarDetails["thumbnailImageUrl"]?.ToString();
                }

                if (string.IsNullOrWhiteSpace(thumbnailUrl))
                    OnLog?.Invoke($"[AVATAR] No preview image URL was available for {displayName}.");

                var previewImage = string.IsNullOrWhiteSpace(thumbnailUrl)
                    ? null
                    : await api.GetImageBytesAsync(thumbnailUrl);

                if (!string.IsNullOrWhiteSpace(thumbnailUrl) && previewImage == null)
                    OnLog?.Invoke($"[AVATAR] Preview image download failed for {displayName}.");
                else if (previewImage != null)
                    OnLog?.Invoke($"[AVATAR] Preview image ready for {displayName} ({previewImage.Length:N0} bytes).");

                OnAvatarObserved?.Invoke(new AvatarObservation
                {
                    DisplayName = displayName,
                    UserId = wearerUserId,
                    AvatarName = avatarName,
                    AvatarId = avatarId,
                    ThumbnailUrl = thumbnailUrl,
                    PreviewImage = previewImage
                });
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[AVATAR] Could not retrieve avatar preview for {displayName}: {ex.Message}");
            }
        }

        // Scans everyone currently in the instance and bans anyone already wearing the
        // given (typically newly blacklisted) avatar name or id.
        private async Task ScanPresentPlayersForAvatarAsync(string avatarEntry)
        {
            if (isStopped || string.IsNullOrWhiteSpace(avatarEntry))
                return;

            var value = avatarEntry.Trim();

            List<(string Name, string? AvName, string? AvId)> snapshot;
            lock (playerStateLock)
            {
                snapshot = currentAvatarByName
                    .Select(kvp => (kvp.Key, kvp.Value.Name, kvp.Value.Id))
                    .ToList();
            }

            foreach (var (name, avName, avId) in snapshot)
            {
                if (isStopped)
                    return;

                var isMatch =
                    (!string.IsNullOrWhiteSpace(avId) && string.Equals(avId, value, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(avName) && string.Equals(avName, value, StringComparison.OrdinalIgnoreCase));
                if (!isMatch)
                    continue;

                var safeName = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;

                // Never act on our own avatar.
                if (!string.IsNullOrWhiteSpace(authenticatedDisplayName) &&
                    string.Equals(safeName, authenticatedDisplayName, StringComparison.OrdinalIgnoreCase))
                    continue;

                await BanAvatarWearerAsync(safeName, value);
            }
        }

        // Resolves the wearer's user id and bans them for a matched blacklisted avatar,
        // deferring until the id is known if the player has not been tracked yet.
        private async Task BanAvatarWearerAsync(string safeName, string matched)
        {
            var userId = FindUserIdByDisplayName(safeName);
            if (string.IsNullOrWhiteSpace(userId))
            {
                // We saw the avatar but the player's user id isn't known yet (no join
                // line seen). Remember it so the ban fires once they are tracked.
                lock (playerStateLock)
                {
                    pendingAvatarBans[safeName] = matched;
                }
                OnLog?.Invoke($"[AVATAR] {safeName} is wearing blacklisted avatar '{matched}', but their user id is not known yet.");
                return;
            }

            lock (playerStateLock)
            {
                if (processedAvatarBanUsers.Contains(userId))
                    return;
                processedAvatarBanUsers.Add(userId);
            }

            var reason = $"wearing blacklisted avatar '{matched}'";
            OnBlacklistedAvatarMatched?.Invoke(safeName, userId, matched);
            OnLog?.Invoke($"[AVATAR] {safeName} matched blacklisted avatar '{matched}'.");
            await TryAutoBanAsync(safeName, userId, reason);
        }

        // Manual staff actions we surface to Discord, mapped to a friendly label.
        private static readonly Dictionary<string, string> auditActionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["group.instance.warn"] = "Instance Warn",
            ["group.instance.kick"] = "Instance Kick",
            ["group.user.ban"] = "Group Ban",
            ["group.user.unban"] = "Group Unban",
        };

        // Polls the owned group's audit log so manual staff warns/kicks/bans/unbans can be
        // forwarded to Discord. The first fetch only establishes a baseline so historical
        // entries are not re-posted; subsequent polls emit anything newer.
        private async Task StartAuditLogPollingLoopAsync(System.Threading.CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(banTargetGroupId))
                return;

            var baseline = await api.GetGroupAuditLogsAsync(banTargetGroupId, 50);
            lastAuditLogSeenUtc = NewestAuditTimestamp(baseline, DateTime.UtcNow);
            OnLog?.Invoke("[STAFF-LOG] Watching group audit log for manual staff actions.");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(auditLogPollInterval, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested || isStopped)
                    return;

                try
                {
                    var entries = await api.GetGroupAuditLogsAsync(banTargetGroupId, 50);
                    EmitNewStaffModerationEntries(entries);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[STAFF-LOG] Audit log poll failed: {ex.Message}");
                }
            }
        }

        private static DateTime NewestAuditTimestamp(Newtonsoft.Json.Linq.JArray entries, DateTime fallback)
        {
            var newest = DateTime.MinValue;
            foreach (var entry in entries)
            {
                if (TryParseAuditTimestamp(entry?["created_at"]?.ToString(), out var ts) && ts > newest)
                    newest = ts;
            }
            return newest == DateTime.MinValue ? fallback : newest;
        }

        private static bool TryParseAuditTimestamp(string? value, out DateTime utc)
        {
            return DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out utc);
        }

        private void EmitNewStaffModerationEntries(Newtonsoft.Json.Linq.JArray entries)
        {
            // Entries arrive newest-first; collect the new ones then emit oldest-first so
            // the Discord order matches the real sequence of events.
            var newEvents = new List<StaffModerationEvent>();
            var maxTs = lastAuditLogSeenUtc;

            foreach (var entry in entries)
            {
                var eventType = entry?["eventType"]?.ToString();
                if (string.IsNullOrWhiteSpace(eventType) || !auditActionLabels.TryGetValue(eventType, out var label))
                    continue;

                if (!TryParseAuditTimestamp(entry?["created_at"]?.ToString(), out var ts) || ts <= lastAuditLogSeenUtc)
                    continue;

                var id = entry?["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(id) && !seenAuditLogIds.Add(id))
                    continue;

                if (ts > maxTs)
                    maxTs = ts;

                var location = entry?["data"]?["location"]?.ToString();
                newEvents.Add(new StaffModerationEvent
                {
                    Actor = entry?["actorDisplayName"]?.ToString() ?? "Unknown staff",
                    EventType = eventType,
                    ActionLabel = label,
                    Description = entry?["description"]?.ToString() ?? string.Empty,
                    TargetId = entry?["targetId"]?.ToString(),
                    Location = string.IsNullOrWhiteSpace(location) ? null : location,
                    CreatedAtUtc = ts
                });
            }

            lastAuditLogSeenUtc = maxTs;

            // Everything already emitted has ts <= lastAuditLogSeenUtc, so clearing the id
            // set once it grows large cannot cause re-posts.
            if (seenAuditLogIds.Count > 500)
                seenAuditLogIds.Clear();

            for (var i = newEvents.Count - 1; i >= 0; i--)
                OnStaffModerationDetected?.Invoke(newEvents[i]);
        }

        private string? FindUserIdByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            lock (playerStateLock)
            {
                foreach (var kvp in userIdToDisplayName)
                {
                    if (string.Equals(kvp.Value, displayName, StringComparison.OrdinalIgnoreCase))
                        return kvp.Key;
                }
            }

            return null;
        }

        private string? FindAvatarBlacklistMatch(string? avatarName, string? avatarId)
        {
            foreach (var entry in configuredBlacklistedAvatars)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                var value = entry.Trim();

                if (!string.IsNullOrWhiteSpace(avatarId) &&
                    string.Equals(avatarId, value, StringComparison.OrdinalIgnoreCase))
                    return value;

                if (!string.IsNullOrWhiteSpace(avatarName) &&
                    string.Equals(avatarName, value, StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            return null;
        }

        private async Task TryAutoBanAsync(string displayName, string userId, string reason)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            lock (playerStateLock)
            {
                if (processedAutoBanUsers.Contains(userId))
                    return;
            }

            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;

            foreach (var groupId in configuredWhitelistedGroupIds)
            {
                if (await api.IsUserInGroupAsync(userId, groupId))
                {
                    lock (playerStateLock)
                    {
                        processedAutoBanUsers.Add(userId);
                    }
                    OnLog?.Invoke($"[AUTO-BAN] Skipped {safeName} ({userId}): member of whitelisted group {groupId}.");
                    return;
                }
            }

            // Locally configured group staff are never banned (zero API cost).
            if (IsLocalStaff(userId, safeName))
            {
                lock (playerStateLock)
                {
                    processedAutoBanUsers.Add(userId);
                }
                OnLog?.Invoke($"[AUTO-BAN] Skipped {safeName} ({userId}): listed as group staff.");
                return;
            }

            // Never ban group staff/management, regardless of why they were flagged.
            if (!string.IsNullOrWhiteSpace(banTargetGroupId))
            {
                var isStaff = await api.IsUserGroupStaffAsync(banTargetGroupId, userId);
                if (isStaff)
                {
                    lock (playerStateLock)
                    {
                        processedAutoBanUsers.Add(userId);
                    }
                    OnLog?.Invoke($"[AUTO-BAN] Skipped {safeName} ({userId}): user has staff-level group access.");
                    return;
                }
            }

            lock (playerStateLock)
            {
                processedAutoBanUsers.Add(userId);
            }
            OnPlayerBanned?.Invoke(safeName, userId, reason);

            if (string.IsNullOrWhiteSpace(banTargetGroupId))
            {
                OnLog?.Invoke($"[AUTO-BAN] {safeName} flagged for {reason}. (No ban target group configured)");
                return;
            }

            var groupName = await api.GetGroupNameAsync(banTargetGroupId) ?? banTargetGroupId;
            var (success, error) = await api.BanUserFromGroup(banTargetGroupId, userId);

            if (success)
                OnLog?.Invoke($"[AUTO-BAN] {safeName} has been banned from {groupName}.");
            else if (error.Contains("User is already banned", StringComparison.OrdinalIgnoreCase))
                OnLog?.Invoke($"[AUTO-BAN] {safeName} was already banned from {groupName}.");
            else
                OnLog?.Invoke($"[AUTO-BAN] Ban failed for {safeName} from {groupName}. Error: {error}");
        }

        private static List<string> LoadRestrictedAreaMarkers()
        {
            var path = RuntimePaths.GetFile("restricted_area_markers.txt");

            // Built-in markers are always active. The external file is NOT created for
            // downloaders; it is only read if it already exists (private customization).
            var builtInMarkers = new[]
            {
                "staff area",
                "staff only",
                "restricted area",
                "admin area",
                "teleport exploit"
            };

            var customMarkers = File.Exists(path)
                ? File.ReadAllLines(path)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .ToList()
                : new List<string>();

            return builtInMarkers
                .Concat(customMarkers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> LoadRipSuspicionMarkers()
        {
            var path = RuntimePaths.GetFile("rip_suspicion_markers.txt");
            var builtInMarkers = new[]
            {
                "ripper",
                "asset extractor",
                "bundle extractor",
                "world export",
                "udon decompiler",
                "client spoof"
            };

            // Built-in markers are always active. The external file is NOT created for
            // downloaders; it is only read if it already exists (private customization).
            var customMarkers = File.Exists(path)
                ? File.ReadAllLines(path)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .ToList()
                : new List<string>();

            return builtInMarkers
                .Concat(customMarkers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, List<RestrictedAreaConfig>> LoadWorldRestrictedAreas()
        {
            var path = RuntimePaths.GetFile("world_restricted_areas.txt");
            var result = new Dictionary<string, List<RestrictedAreaConfig>>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(path))
                return result;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    var worldId = parts[0].Trim();
                    var method = parts[1].Trim().ToLower();
                    var pattern = parts[2].Trim();
                    var description = parts[3].Trim();

                    if (!result.ContainsKey(worldId))
                        result[worldId] = new List<RestrictedAreaConfig>();

                    result[worldId].Add(new RestrictedAreaConfig
                    {
                        DetectionMethod = method,
                        Pattern = pattern,
                        Description = description
                    });
                }
            }

            return result;
        }

        private static string LoadBanTargetGroupId()
        {
            var path = RuntimePaths.GetFile("owned_group.txt");
            if (!File.Exists(path))
                return string.Empty;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                    return line;
            }

            return string.Empty;
        }

        private string? FindBlacklistMatch(string displayName, string userId)
        {
            // Pre-normalize the display name once so word/name entries are matched against
            // a spacing- and leetspeak-resistant version of the name.
            var normalizedName = NormalizeForMatch(displayName);

            foreach (var raw in Blacklist.Entries)
            {
                var entry = raw?.Trim();
                if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith("#"))
                    continue;

                // ID entries (usr_/grp_/gmem_) are matched literally against the raw id and
                // name - normalization would corrupt the id's digits and separators.
                if (IsIdEntry(entry))
                {
                    if (!string.IsNullOrWhiteSpace(userId) &&
                        userId.Contains(entry, StringComparison.OrdinalIgnoreCase))
                        return entry;
                    if (!string.IsNullOrWhiteSpace(displayName) &&
                        displayName.Contains(entry, StringComparison.OrdinalIgnoreCase))
                        return entry;
                    continue;
                }

                // Name/word entries: match the normalized entry anywhere inside the
                // normalized name. This defeats spacing ("b a d"), separators
                // ("b.a.d"), leetspeak ("b4d", "b@d"), and stretched letters ("baaad").
                var normalizedEntry = NormalizeForMatch(entry);
                if (!string.IsNullOrEmpty(normalizedEntry) &&
                    normalizedName.Contains(normalizedEntry, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static bool IsIdEntry(string entry)
            => entry.StartsWith("usr_", StringComparison.OrdinalIgnoreCase)
            || entry.StartsWith("grp_", StringComparison.OrdinalIgnoreCase)
            || entry.StartsWith("gmem_", StringComparison.OrdinalIgnoreCase);

        // Scans a player's username, bio, and status against the warning watchlists.
        // Warning only: on a match it reports the player to the automod log but never bans.
        // Word/phrase entries from blacklist.txt are username warnings; explicit IDs still
        // use the auto-ban path above. Bio/status require one profile fetch.
        private async Task CheckProfileFlagsAsync(string displayName, string userId, System.Threading.CancellationToken token)
        {
            if ((FlaggedWords.Entries.Count == 0 && Blacklist.Entries.Count == 0) || string.IsNullOrWhiteSpace(userId))
                return;

            lock (playerStateLock)
            {
                if (!flaggedProfileUsers.Add(userId))
                    return;
            }

            // Do not flag our own staff.
            if (IsLocalStaff(userId, displayName))
                return;

            var match = FindUsernameBlacklistWord(displayName);
            var matchSource = match == null ? "flagged word" : "username blacklist entry";
            if (match == null)
                match = FindFlaggedWord(displayName);
            if (match == null)
            {
                if (api.IsCoolingDown)
                    return;

                var user = await api.GetUserAsync(userId);
                if (token.IsCancellationRequested)
                    return;
                if (user["error"] == null)
                {
                    match = FindFlaggedWord(user["bio"]?.ToString())
                        ?? FindFlaggedWord(user["statusDescription"]?.ToString())
                        ?? FindFlaggedWord(user["status"]?.ToString());
                }
            }

            if (match == null || token.IsCancellationRequested)
                return;

            var worldName = await ResolveWorldNameAsync(lastLoggedWorldId ?? string.Empty);
            var groupName = string.IsNullOrWhiteSpace(banTargetGroupId)
                ? "your group"
                : (await api.GetGroupNameAsync(banTargetGroupId) ?? banTargetGroupId);

            OnLog?.Invoke($"[WATCHWORD] {displayName} matched {matchSource} '{match}' in their profile.");
            OnProfileFlagged?.Invoke(displayName, match, worldName, groupName);
        }

        private static string? FindUsernameBlacklistWord(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            var normalizedName = NormalizeForMatch(displayName);
            foreach (var raw in Blacklist.Entries)
            {
                var entry = raw?.Trim();
                if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith("#") || IsIdEntry(entry))
                    continue;

                var normalizedEntry = NormalizeForMatch(entry);
                if (!string.IsNullOrEmpty(normalizedEntry) &&
                    normalizedName.Contains(normalizedEntry, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        // Returns the first flagged word found inside the given text, using the same
        // evasion-resistant normalization as the ban blacklist.
        private static string? FindFlaggedWord(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var normalizedText = NormalizeForMatch(text);
            foreach (var raw in FlaggedWords.Entries)
            {
                var entry = raw?.Trim();
                if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith("#"))
                    continue;

                var normalizedEntry = NormalizeForMatch(entry);
                if (!string.IsNullOrEmpty(normalizedEntry) &&
                    normalizedText.Contains(normalizedEntry, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        // Folds a name down to bare letters/digits so common evasion tricks still match:
        //   - lowercased
        //   - common leetspeak mapped to letters (0->o, 1->i, 3->e, 4->a, @->a, $->s, ...)
        //   - every non-alphanumeric character (spaces, dots, symbols) removed
        //   - runs of the same character collapsed to one ("baaad" -> "bad")
        private static string NormalizeForMatch(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Decompose accented characters (e.g. "à" -> "a" + combining mark) so the
            // combining marks can be dropped, defeating diacritic-based evasion.
            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(decomposed.Length);
            var last = '\0';
            foreach (var rawCh in decomposed)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(rawCh)
                    == System.Globalization.UnicodeCategory.NonSpacingMark)
                    continue;

                var ch = rawCh switch
                {
                    '0' or '(' or ')' => 'o',
                    '1' or '!' or '|' => 'i',
                    '3' or '£' or '€' => 'e',
                    '4' or '@' or 'ᴀ' => 'a',
                    '5' or '$' => 's',
                    '6' or '9' => 'g',
                    '7' or '+' => 't',
                    '8' => 'b',
                    _ => rawCh
                };

                if (!char.IsLetterOrDigit(ch))
                    continue;

                if (ch == last)
                    continue; // collapse stretched/repeated characters

                sb.Append(ch);
                last = ch;
            }

            return sb.ToString();
        }

        private async Task<string> ResolveWorldNameAsync(string worldId)
        {
            try
            {
                var world = await api.GetWorldAsync(worldId);
                if (world["error"] != null)
                    return worldId;

                var name = world["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;

                return worldId;
            }
            catch
            {
                return worldId;
            }
        }

        private bool ShouldLogUserEvent(string userId, bool isJoin)
        {
            lock (playerStateLock)
            {
                var map = isJoin ? lastJoinLogAt : lastLeftLogAt;
                var now = DateTime.UtcNow;

                if (map.TryGetValue(userId, out var lastAt) && now - lastAt < duplicateUserEventWindow)
                    return false;

                map[userId] = now;
                return true;
            }
        }
    }
}
