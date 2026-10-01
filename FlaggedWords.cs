using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VRChatInstanceLogger
{
    // A shared, warning-only word list. Anyone whose username, bio, or status contains a
    // listed word is flagged to the automod log as a "potential troublemaker" — never
    // banned. Stored in the current user's app-data folder.
    public static class FlaggedWords
    {
        private static readonly string filePath =
            RuntimePaths.GetFile("flagged_words.txt");

        public static List<string> Entries = new List<string>();

        static FlaggedWords()
        {
            Load();
        }

        public static void Load()
        {
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath,
@"# Flagged words - one entry per line. Anyone whose username, bio, or status
# contains a listed word is reported to the automod log as a potential
# troublemaker. This list ONLY warns - it never bans or kicks.
#
# Matching is partial and evasion-resistant (spacing, leetspeak and symbols are
# ignored), so listing  badword  also catches ""b a d w o r d"" and ""b4dw0rd"".
#
# Lines starting with # are ignored. Example (ignored):
# badword
");
            }

            Entries = new List<string>(File.ReadAllLines(filePath));
        }

        private static IEnumerable<string> ActiveEntries()
            => Entries
                .Select(e => e?.Trim() ?? string.Empty)
                .Where(e => e.Length > 0 && !e.StartsWith("#"));

        public static bool Add(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            var value = word.Trim();
            if (ActiveEntries().Contains(value, StringComparer.OrdinalIgnoreCase))
                return false;

            var needsNewline = File.Exists(filePath)
                && new FileInfo(filePath).Length > 0
                && !File.ReadAllText(filePath).EndsWith("\n");

            File.AppendAllText(filePath, (needsNewline ? Environment.NewLine : "") + value + Environment.NewLine);
            Load();
            return true;
        }

        public static bool Remove(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            var value = word.Trim();
            if (!File.Exists(filePath))
                return false;

            var lines = File.ReadAllLines(filePath).ToList();
            var remaining = lines
                .Where(l => !string.Equals(l?.Trim(), value, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (remaining.Count == lines.Count)
                return false;

            File.WriteAllLines(filePath, remaining);
            Load();
            return true;
        }

        public static bool Clear()
        {
            if (!ActiveEntries().Any())
                return false;

            var comments = File.Exists(filePath)
                ? File.ReadAllLines(filePath).Where(line => line.TrimStart().StartsWith("#")).ToList()
                : new List<string>();
            File.WriteAllLines(filePath, comments);
            Load();
            return true;
        }
    }
}
