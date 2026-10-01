using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VRChatInstanceLogger
{
    // Stores each VRChat account's blacklisted groups separately under the current
    // Windows user's private app-data folder, keyed by the logged-in VRChat user id.
    // This guarantees one person's blacklist never shows up for a different account.
    public static class AccountBlacklist
    {
        public const int MaxEntries = 100;

        private static readonly string StoreFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatInstanceLogger", "blacklists");

        private static readonly Regex GroupIdPattern =
            new Regex("((?:grp|gmem)_[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase);

        public static bool IsValidGroupId(string? value)
            => !string.IsNullOrWhiteSpace(value) && GroupIdPattern.IsMatch(value);

        private static string SanitizeUserId(string userId)
        {
            var safe = new string(userId.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
        }

        public static string GetStorePath(string userId)
            => Path.Combine(StoreFolder, SanitizeUserId(userId) + ".txt");

        // Loads the blacklist for a single account. The very first time an account is
        // used on this machine, any legacy shared list that was sitting next to the app
        // is imported once (and only for the first account to claim it). Distributed
        // copies have no legacy file, so new users always start with an empty list.
        public static List<string> Load(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new List<string>();

            var path = GetStorePath(userId);
            if (!File.Exists(path))
                TryImportLegacyList(userId, path);

            return ReadIds(path).Take(MaxEntries).ToList();
        }

        // Adds a group id to a single account's blacklist file. Returns false if the id
        // is blank/invalid or already present.
        public static bool Add(string userId, string groupId)
        {
            if (string.IsNullOrWhiteSpace(userId) || !IsValidGroupId(groupId))
                return false;

            var id = GroupIdPattern.Match(groupId).Groups[1].Value;

            var existing = Load(userId);
            if (existing.Contains(id, StringComparer.OrdinalIgnoreCase))
                return false;
            if (existing.Count >= MaxEntries)
                return false;

            var path = GetStorePath(userId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var needsNewline = File.Exists(path)
                && new FileInfo(path).Length > 0
                && !File.ReadAllText(path).EndsWith("\n");

            File.AppendAllText(path, (needsNewline ? Environment.NewLine : "") + id + Environment.NewLine);
            return true;
        }

        // Removes a group id from a single account's blacklist file. Returns false if the
        // id was not present.
        public static bool Remove(string userId, string groupId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(groupId))
                return false;

            var path = GetStorePath(userId);
            var existing = ReadIds(path);

            var remaining = existing
                .Where(id => !string.Equals(id, groupId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (remaining.Count == existing.Count)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, remaining);
            return true;
        }

        public static bool Clear(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var path = GetStorePath(userId);
            if (!File.Exists(path) || ReadIds(path).Count == 0)
                return false;

            File.WriteAllText(path, string.Empty);
            return true;
        }

        private static List<string> ReadIds(string path)
        {
            var result = new List<string>();
            if (!File.Exists(path))
                return result;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var match = GroupIdPattern.Match(line);
                if (!match.Success)
                    continue;

                var id = match.Groups[1].Value;
                if (!result.Contains(id, StringComparer.OrdinalIgnoreCase))
                    result.Add(id);
            }

            return result;
        }

        // One-time import of the old shared "group_blacklist.txt[.txt]" file that used to
        // live next to the app. Only the first account to log in on a machine that still
        // has that file will inherit it; everyone else starts empty.
        private static void TryImportLegacyList(string userId, string destinationPath)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var legacyPreferred = Path.Combine(baseDir, "group_blacklist.txt.txt");
                var legacyOld = Path.Combine(baseDir, "group_blacklist.txt");
                var legacyPath = File.Exists(legacyPreferred) ? legacyPreferred
                                : File.Exists(legacyOld) ? legacyOld
                                : null;

                if (legacyPath == null)
                    return; // Distributed copies have no legacy list — start empty.

                Directory.CreateDirectory(StoreFolder);

                var claimMarker = Path.Combine(StoreFolder, ".legacy_imported");
                if (File.Exists(claimMarker))
                    return; // Already claimed by the first account on this machine.

                var ids = ReadIds(legacyPath);
                if (ids.Count > 0)
                    File.WriteAllLines(destinationPath, ids);

                File.WriteAllText(claimMarker, userId);
            }
            catch
            {
                // If migration fails for any reason, the account simply starts empty.
            }
        }
    }
}
