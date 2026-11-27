using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StrmAssistant.Common
{
    internal static class PathBoundary
    {
        public static bool IsSameOrDescendant(string candidatePath, string rootPath)
        {
            try
            {
                var candidate = Normalize(candidatePath);
                var root = Normalize(rootPath);
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                if (string.Equals(candidate, root, comparison)) return true;

                var rootPrefix = Path.EndsInDirectorySeparator(root)
                    ? root
                    : root + Path.DirectorySeparatorChar;
                return candidate.StartsWith(rootPrefix, comparison);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return false;
            }
        }

        public static bool IsLocalFileInScope(string candidatePath, IEnumerable<string> rootPaths)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || rootPaths == null) return false;

            var candidate = candidatePath;
            if (Uri.TryCreate(candidatePath, UriKind.Absolute, out var uri))
            {
                if (!uri.IsFile) return false;
                candidate = uri.LocalPath;
            }

            return rootPaths.Any(root => !string.IsNullOrWhiteSpace(root) &&
                                         IsSameOrDescendant(candidate, root));
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));

            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && fullPath.Length > root.Length)
            {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return fullPath;
        }
    }
}
