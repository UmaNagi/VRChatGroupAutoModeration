using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VRChatInstanceLogger
{
    // Stores each VRChat account's blacklisted avatars (by avatar name or avatar id)
    // privately under the current Windows user's app-data folder, keyed by the logged-in
    // VRChat user id. Kept separate from the group blacklist so the two never mix.
    public static class AccountAvatarBlacklist
    {
        private static readonly string StoreFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRChatInstanceLogger", "blacklists");

        private static string SanitizeUserId(string userId)
        {
            var safe = new string(userId.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "unknown" : safe;
        }

        public static string GetStorePath(string userId)
            => Path.Combine(StoreFolder, SanitizeUserId(userId) + ".avatars.txt");

        public static List<string> Load(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new List<string>();

            return ReadEntries(GetStorePath(userId));
        }

        // Adds an avatar name or id to a single account's list. Returns false if the
        // entry is blank or already present (case-insensitive).
        public static bool Add(string userId, string entry)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(entry))
                return false;

            var value = entry.Trim();

            var existing = Load(userId);
            if (existing.Contains(value, StringComparer.OrdinalIgnoreCase))
                return false;

            var path = GetStorePath(userId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var needsNewline = File.Exists(path)
                && new FileInfo(path).Length > 0
                && !File.ReadAllText(path).EndsWith("\n");

            File.AppendAllText(path, (needsNewline ? Environment.NewLine : "") + value + Environment.NewLine);
            return true;
        }

        // Removes an avatar name or id from a single account's list. Returns false if the
        // entry was not present.
        public static bool Remove(string userId, string entry)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(entry))
                return false;

            var value = entry.Trim();
            var path = GetStorePath(userId);
            var existing = ReadEntries(path);

            var remaining = existing
                .Where(e => !string.Equals(e, value, StringComparison.OrdinalIgnoreCase))
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
            if (!File.Exists(path) || ReadEntries(path).Count == 0)
                return false;

            File.WriteAllText(path, string.Empty);
            return true;
        }

        private static List<string> ReadEntries(string path)
        {
            var result = new List<string>();
            if (!File.Exists(path))
                return result;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                if (!result.Contains(line, StringComparer.OrdinalIgnoreCase))
                    result.Add(line);
            }

            return result;
        }
    }
}
