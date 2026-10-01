using System;
using System.Collections.Generic;
using System.IO;

namespace VRChatInstanceLogger
{
    public static class Blacklist
    {
        private static readonly string filePath =
            RuntimePaths.GetFile("blacklist.txt");
        public static List<string> Entries = new List<string>();

        static Blacklist()
        {
            Load();
        }

        public static void Load()
        {
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath,
@"# Blacklist - one entry per line. User/group IDs are auto-banned from your
# owned group; name and word/phrase entries warn staff when seen in an instance.
#
# You can list any of these (one per line):
#   - A user ID        (starts with usr_)
#   - A display name, OR any word/phrase to warn staff about inside a name
#   - A group ID       (starts with grp_)
#
# NAME MATCHING IS PARTIAL: if a listed word appears ANYWHERE inside a player's
# name, they match. Example: listing  badword  matches ""badword"",
# ""xXbadwordXx"", and ""the_badword_guy"".
#
# Do NOT use real players in examples. The sample lines below are ignored
# because they start with #:
#
# usr_00000000-0000-0000-0000-000000000000
# ExampleBadName
# badword
# grp_00000000-0000-0000-0000-000000000000
");
            }

            Entries = new List<string>(File.ReadAllLines(filePath));
        }

        public static bool IsBlacklisted(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (var entry in Entries)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                if (entry.StartsWith("#")) continue;

                if (text.Contains(entry, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
