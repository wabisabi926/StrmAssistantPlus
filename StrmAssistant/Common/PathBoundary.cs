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

            try
            {
                var candidate = candidatePath;
                if (Uri.TryCreate(candidatePath, UriKind.Absolute, out var uri))
                {
                    if (!uri.IsFile) return false;
                    candidate = uri.LocalPath;
                }

                var resolvedCandidate = ResolveFinalPath(candidate);
                return rootPaths.Any(root => !string.IsNullOrWhiteSpace(root) &&
                                             IsSameOrDescendant(resolvedCandidate, ResolveFinalPath(root)));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                       ex is PathTooLongException || ex is IOException ||
                                       ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                return false;
            }
        }

        internal static string ResolveFinalPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return Normalize(fullPath);

            var current = root;
            current = ResolveLink(current);
            var relative = fullPath.Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(relative)) return Normalize(current);

            foreach (var segment in relative.Split(new[]
                     { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                current = ResolveLink(current);
            }

            return Normalize(current);

            string ResolveLink(string candidate)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(candidate);
                }
                catch (FileNotFoundException)
                {
                    return candidate;
                }
                catch (IOException)
                {
                    return candidate;
                }
                catch (UnauthorizedAccessException)
                {
                    return candidate;
                }

                if ((attributes & FileAttributes.ReparsePoint) == 0) return candidate;
                FileSystemInfo target = (attributes & FileAttributes.Directory) != 0
                    ? Directory.ResolveLinkTarget(candidate, true)
                    : File.ResolveLinkTarget(candidate, true);
                if (target == null) throw new IOException("Unable to resolve link target: " + candidate);
                return Path.GetFullPath(target.FullName);
            }
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
