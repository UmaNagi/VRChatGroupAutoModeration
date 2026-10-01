using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VRChatInstanceLogger
{
    public class InstanceDetector
    {
        private readonly string logFolder;
        private string? logPath;
        private long lastSize = 0;
        private bool hasReportedMissingLogs = false;
        private string? lastWorldId;
        private string? lastInstanceId;
        private string? lastPlayerJoinUserId;
        private DateTime lastPlayerJoinAtUtc;

        public event Action<string, string>? OnInstanceChanged;
        public event Action<string, string>? OnPlayerJoined;
        public event Action<string, string>? OnPlayerLeft;
        public event Action<string>? OnLogLine;
        public event Action<string>? OnDebug;
        // (displayName, avatarName, avatarId) — avatarName or avatarId may be null.
        public event Action<string, string?, string?>? OnAvatarObserved;

        public string LogFolder => logFolder;

        public InstanceDetector()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var primaryPath = Path.GetFullPath(Path.Combine(localAppData, "..", "LocalLow", "VRChat", "VRChat"));
            var fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "VRChat", "VRChat");

            if (Directory.Exists(primaryPath))
                logFolder = primaryPath;
            else if (Directory.Exists(fallbackPath))
                logFolder = fallbackPath;
            else
                logFolder = primaryPath;
        }

        private string? GetNewestLogFile(string folder)
        {
            if (!Directory.Exists(folder))
                return null;

            var files = Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly)
                .Where(file => !string.Equals(Path.GetFileName(file), "VRChatInstanceLogger.txt", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Length == 0)
                return null;

            var preferred = files
                .Where(file => Path.GetFileName(file).Contains("output_log", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(file).Contains("vrchat", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(file => File.GetLastWriteTime(file))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;

            var nonEmptyFiles = Array.FindAll(files, file =>
            {
                try
                {
                    return new FileInfo(file).Length > 0;
                }
                catch
                {
                    return false;
                }
            });

            if (nonEmptyFiles.Length > 0)
                files = nonEmptyFiles;

            Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
            return files[0];
        }

        // Returns the latest world/instance location written by VRChat so logging can
        // begin for an already-open instance instead of waiting for a rejoin event.
        public bool TryGetMostRecentInstance(out string worldId, out string instanceId)
        {
            worldId = string.Empty;
            instanceId = string.Empty;

            try
            {
                var newestLogPath = GetNewestLogFile(logFolder);
                if (string.IsNullOrWhiteSpace(newestLogPath) || !File.Exists(newestLogPath))
                    return false;

                using var stream = new FileStream(newestLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!IsActiveInstanceLine(line))
                        continue;

                    var match = Regex.Match(line, @"wrld_[A-Za-z0-9\-]+:[^\s]+");
                    if (!match.Success)
                        continue;

                    var fullLocation = match.Value;
                    var separatorIndex = fullLocation.IndexOf(':');
                    if (separatorIndex <= 0 || separatorIndex >= fullLocation.Length - 1)
                        continue;

                    worldId = fullLocation.Substring(0, separatorIndex);
                    instanceId = fullLocation.Substring(separatorIndex + 1).TrimEnd(',', '.', ';');
                }

                return !string.IsNullOrWhiteSpace(worldId) && !string.IsNullOrWhiteSpace(instanceId);
            }
            catch
            {
                worldId = string.Empty;
                instanceId = string.Empty;
                return false;
            }
        }

        // Replays player joins/leaves recorded after the latest occurrence of the
        // specified instance. This fills the lobby list when logging begins after
        // players are already in the room, while StartWatching handles new events.
        public IReadOnlyList<(string DisplayName, string UserId)> GetCurrentInstancePlayers(string worldId, string instanceId)
        {
            var players = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var newestLogPath = GetNewestLogFile(logFolder);
                if (string.IsNullOrWhiteSpace(newestLogPath) || !File.Exists(newestLogPath))
                    return Array.Empty<(string DisplayName, string UserId)>();

                var targetSignature = NormalizeInstanceSignature(worldId, instanceId);
                var currentInstanceStarted = false;
                using var stream = new FileStream(newestLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var locationMatch = Regex.Match(line, @"wrld_[A-Za-z0-9\-]+:[^\s]+");
                    if (locationMatch.Success)
                    {
                        if (!IsActiveInstanceLine(line))
                            continue;

                        currentInstanceStarted = string.Equals(
                            NormalizeInstanceSignature(locationMatch.Value),
                            targetSignature,
                            StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!currentInstanceStarted)
                        continue;

                    var playerJoinMatch = Regex.Match(line, @"OnPlayerJoined\s+(.+?)\s+\((usr_[A-Za-z0-9\-]+)\)");
                    if (playerJoinMatch.Success)
                    {
                        players[playerJoinMatch.Groups[2].Value.Trim()] = playerJoinMatch.Groups[1].Value.Trim();
                        continue;
                    }

                    var playerLeaveMatch = Regex.Match(line, @"OnPlayerLeft\s+(.+?)\s+\((usr_[A-Za-z0-9\-]+)\)");
                    if (playerLeaveMatch.Success)
                        players.Remove(playerLeaveMatch.Groups[2].Value.Trim());
                }
            }
            catch
            {
                return Array.Empty<(string DisplayName, string UserId)>();
            }

            return players
                .Select(player => (DisplayName: player.Value, UserId: player.Key))
                .ToList();
        }

        private static string NormalizeInstanceSignature(string worldId, string instanceId)
        {
            var signature = $"{worldId.Trim()}:{instanceId.Trim()}";
            return NormalizeInstanceSignature(signature);
        }

        private static string NormalizeInstanceSignature(string location)
        {
            var signature = location.Trim();
            var qualifierIndex = signature.IndexOf('~');
            return qualifierIndex > 0 ? signature.Substring(0, qualifierIndex) : signature;
        }

        public async Task StartWatching(System.Threading.CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var newestLogPath = GetNewestLogFile(logFolder);
                    if (string.IsNullOrWhiteSpace(newestLogPath))
                    {
                        if (!hasReportedMissingLogs)
                        {
                            hasReportedMissingLogs = true;
                            OnDebug?.Invoke($"[DETECTOR] Waiting for VRChat logs in: {logFolder}");
                        }

                        logPath = null;
                        lastSize = 0;
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    hasReportedMissingLogs = false;

                    if (!string.Equals(logPath, newestLogPath, StringComparison.OrdinalIgnoreCase))
                    {
                        logPath = newestLogPath;

                        // Start at end-of-file to avoid replaying stale historical join/leave lines.
                        // Replayed old lines can create ghost users that are no longer in the instance.
                        long initialSize = 0;
                        try
                        {
                            initialSize = new FileInfo(logPath).Length;
                        }
                        catch
                        {
                            initialSize = 0;
                        }

                        lastSize = Math.Max(0, initialSize);
                        OnDebug?.Invoke($"[DETECTOR] Watching {Path.GetFileName(logPath)} (size={initialSize} bytes, offset={lastSize}).");
                    }

                    if (!File.Exists(logPath))
                    {
                        logPath = null;
                        lastSize = 0;
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    long newSize = new FileInfo(logPath).Length;

                    if (newSize < lastSize)
                    {
                        lastSize = 0;
                        OnDebug?.Invoke($"[DETECTOR] Log file shrank, restarting read from beginning: {Path.GetFileName(logPath)}.");
                    }

                    if (newSize > lastSize)
                    {
                        using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs))
                        {
                            fs.Seek(lastSize, SeekOrigin.Begin);

                            string? line;
                            while ((line = await sr.ReadLineAsync()) != null)
                            {
                                OnLogLine?.Invoke(line);
                                ProcessLine(line);
                            }

                            lastSize = fs.Position;
                        }
                    }
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnDebug?.Invoke($"[DETECTOR] Read error: {ex.Message}");
                }

                await Task.Delay(500, cancellationToken);
            }
        }

        private void ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Fallback user detection from VRChat logs when instance API payloads
            // do not include user identities.
            var playerJoinMatch = Regex.Match(line, @"OnPlayerJoined\s+(.+?)\s+\((usr_[A-Za-z0-9\-]+)\)");
            if (playerJoinMatch.Success)
            {
                var playerName = playerJoinMatch.Groups[1].Value.Trim();
                var userId = playerJoinMatch.Groups[2].Value.Trim();

                var nowUtc = DateTime.UtcNow;
                if (!(string.Equals(lastPlayerJoinUserId, userId, StringComparison.OrdinalIgnoreCase) &&
                      (nowUtc - lastPlayerJoinAtUtc).TotalSeconds < 2))
                {
                    lastPlayerJoinUserId = userId;
                    lastPlayerJoinAtUtc = nowUtc;
                    OnPlayerJoined?.Invoke(playerName, userId);
                }
            }

            // Player leave detection from VRChat logs
            var playerLeaveMatch = Regex.Match(line, @"OnPlayerLeft\s+(.+?)\s+\((usr_[A-Za-z0-9\-]+)\)");
            if (playerLeaveMatch.Success)
            {
                var playerName = playerLeaveMatch.Groups[1].Value.Trim();
                var userId = playerLeaveMatch.Groups[2].Value.Trim();
                OnPlayerLeft?.Invoke(playerName, userId);
            }

            // Avatar detection. VRChat logs the avatar a player switches to, e.g.:
            //   [Behaviour] Switching Some Player to avatar SomeAvatarName
            var avatarSwitchMatch = Regex.Match(line, @"Switching (.+?) to avatar (.+?)\s*$");
            if (avatarSwitchMatch.Success)
            {
                var playerName = avatarSwitchMatch.Groups[1].Value.Trim();
                var avatarName = avatarSwitchMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(playerName) && !string.IsNullOrWhiteSpace(avatarName))
                    OnAvatarObserved?.Invoke(playerName, avatarName, null);
            }

            if (!IsActiveInstanceLine(line))
                return;

            // UNIVERSAL VRChat instance regex
            // Matches:
            // wrld_xxxxx:12345
            // wrld_xxxxx:12345~region(us)
            // wrld_xxxxx:12345~region(us)~nonce(abc)
            var match = Regex.Match(line, @"wrld_[A-Za-z0-9\-]+:[^\s]+");

            if (!match.Success)
                return;

            string full = match.Value; // entire instance string

            // Split worldId and instanceId while preserving full instance qualifiers.
            // Example: wrld_xxx:12345~private(usr_xxx)~region(us)
            int separatorIndex = full.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= full.Length - 1)
                return;

            string worldId = full.Substring(0, separatorIndex);
            string instanceId = full.Substring(separatorIndex + 1).TrimEnd(',', '.', ';');

            // VRChat can emit multiple lines for the same destination/join.
            // Suppress duplicate callbacks for the same world+instance pair.
            if (string.Equals(lastWorldId, worldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(lastInstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lastWorldId = worldId;
            lastInstanceId = instanceId;

            OnInstanceChanged?.Invoke(worldId, instanceId);
        }

        private static bool IsActiveInstanceLine(string line)
        {
            if (line.IndexOf("notification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("invite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("invitation", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return Regex.IsMatch(
                line,
                @"\b(joining|joined|entering|entered|loading|connected|current instance|roommanager)\b",
                RegexOptions.IgnoreCase);
        }
    }
}

