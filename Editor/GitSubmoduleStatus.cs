using System;
using System.Collections.Generic;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal sealed class GitSubmoduleStatus
    {
        private GitSubmoduleStatus(IReadOnlyList<GitSubmoduleStatusEntry> entries)
        {
            Entries = entries;
        }

        internal IReadOnlyList<GitSubmoduleStatusEntry> Entries { get; }

        internal static GitSubmoduleStatus Parse(string output)
        {
            List<GitSubmoduleStatusEntry> entries = new List<GitSubmoduleStatusEntry>();
            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                GitSubmoduleStatusEntry entry;
                if (GitSubmoduleStatusEntry.TryParse(line, out entry))
                {
                    entries.Add(entry);
                }
            }

            return new GitSubmoduleStatus(entries);
        }
    }

    internal sealed class GitSubmoduleStatusEntry
    {
        private GitSubmoduleStatusEntry(char prefix, string currentCommit, string path)
        {
            Prefix = prefix;
            CurrentCommit = currentCommit;
            Path = path;
        }

        internal char Prefix { get; }

        internal string CurrentCommit { get; }

        internal string Path { get; }

        internal bool IsMissing => Prefix == '-';

        internal bool IsConflicted => Prefix == 'U';

        internal static bool TryParse(string line, out GitSubmoduleStatusEntry entry)
        {
            entry = null;
            if (line.Length < 43)
            {
                return false;
            }

            char prefix = line[0];
            int commitStart = char.IsWhiteSpace(prefix) ? 1 : 1;
            int commitEnd = line.IndexOf(' ', commitStart);
            if (commitEnd < 0)
            {
                return false;
            }

            string commit = line.Substring(commitStart, commitEnd - commitStart);
            string pathAndDescription = line.Substring(commitEnd + 1);
            int descriptionStart = pathAndDescription.IndexOf(" (", StringComparison.Ordinal);
            string path = descriptionStart >= 0 ? pathAndDescription.Substring(0, descriptionStart) : pathAndDescription;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            entry = new GitSubmoduleStatusEntry(prefix, commit, path.Trim());
            return true;
        }
    }
}
