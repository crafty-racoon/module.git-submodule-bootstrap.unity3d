using System;
using System.IO;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal static class GitRepositoryLocator
    {
        internal static string FindRepositoryRoot(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return string.Empty;
            }

            DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(startPath));
            while (directory != null)
            {
                string gitMetadataPath = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(gitMetadataPath) || File.Exists(gitMetadataPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        internal static string GetRelativeDescendantPath(string ancestorPath, string descendantPath)
        {
            string ancestorFullPath = Path.GetFullPath(ancestorPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string descendantFullPath = Path.GetFullPath(descendantPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(ancestorFullPath, descendantFullPath, comparison))
            {
                return ".";
            }

            string ancestorPrefix = ancestorFullPath + Path.DirectorySeparatorChar;
            if (!descendantFullPath.StartsWith(ancestorPrefix, comparison))
            {
                throw new InvalidOperationException(descendantFullPath + " is not inside repository root " + ancestorFullPath + ".");
            }

            return descendantFullPath.Substring(ancestorPrefix.Length);
        }
    }
}
