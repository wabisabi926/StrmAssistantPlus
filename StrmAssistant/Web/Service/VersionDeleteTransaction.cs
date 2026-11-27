using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StrmAssistant.Web.Service
{
    internal sealed class VersionDeleteTransactionResult
    {
        public VersionDeleteTransactionResult(bool committed, IReadOnlyList<string> retainedStagingPaths,
            string error)
        {
            Committed = committed;
            RetainedStagingPaths = retainedStagingPaths;
            Error = error;
        }

        public bool Committed { get; }
        public IReadOnlyList<string> RetainedStagingPaths { get; }
        public string Error { get; }
    }

    internal static class VersionDeleteTransaction
    {
        private sealed class StagedEntry
        {
            public StagedEntry(string source, string staged, bool isDirectory)
            {
                Source = source;
                Staged = staged;
                IsDirectory = isDirectory;
            }

            public string Source { get; }
            public string Staged { get; }
            public bool IsDirectory { get; }
        }

        public static VersionDeleteTransactionResult Execute(IReadOnlyList<string> filePaths,
            IReadOnlyList<string> directoryPaths, Action<string, string> moveFile,
            Action<string, string> moveDirectory, Action commit, Action<string> deleteStagingDirectory)
        {
            if (moveFile == null) throw new ArgumentNullException(nameof(moveFile));
            if (moveDirectory == null) throw new ArgumentNullException(nameof(moveDirectory));
            if (commit == null) throw new ArgumentNullException(nameof(commit));
            if (deleteStagingDirectory == null) throw new ArgumentNullException(nameof(deleteStagingDirectory));

            var transactionId = Guid.NewGuid().ToString("N");
            var staged = new List<StagedEntry>();
            var stagingRoots = new List<string>();
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

            try
            {
                var files = NormalizeAndValidate(filePaths, false, comparer);
                var directories = NormalizeAndValidate(directoryPaths, true, comparer);

                Stage(files, false);
                Stage(directories, true);

                commit();
                var retained = CleanupStaging();
                return new VersionDeleteTransactionResult(true, retained, null);
            }
            catch (Exception ex)
            {
                var rollbackFailures = Rollback(staged, moveFile, moveDirectory);
                var retained = CleanupStagingAfterRollback(rollbackFailures.Count > 0);
                var error = rollbackFailures.Count == 0
                    ? ex.Message
                    : ex.Message + "; rollback failed: " + string.Join(" | ", rollbackFailures);
                return new VersionDeleteTransactionResult(false, retained, error);
            }

            void Stage(IReadOnlyList<string> sources, bool isDirectory)
            {
                foreach (var source in sources)
                {
                    var parent = Path.GetDirectoryName(source);
                    if (string.IsNullOrEmpty(parent)) throw new IOException("Invalid delete path: " + source);

                    var stagingRoot = Path.Combine(parent, ".strmassistant-delete-" + transactionId);
                    Directory.CreateDirectory(stagingRoot);
                    if (!stagingRoots.Contains(stagingRoot, comparer)) stagingRoots.Add(stagingRoot);

                    var stagedPath = Path.Combine(stagingRoot,
                        (isDirectory ? "dir-" : "file-") + staged.Count.ToString("D4") + "-" + Path.GetFileName(source));
                    File.AppendAllText(Path.Combine(stagingRoot, "operation.log"),
                        $"{source}\t{stagedPath}\t{(isDirectory ? "directory" : "file")}{Environment.NewLine}");
                    if (isDirectory)
                    {
                        moveDirectory(source, stagedPath);
                    }
                    else
                    {
                        moveFile(source, stagedPath);
                    }

                    staged.Add(new StagedEntry(source, stagedPath, isDirectory));
                }
            }

            IReadOnlyList<string> CleanupStaging()
            {
                var retained = new List<string>();
                foreach (var root in stagingRoots)
                {
                    try
                    {
                        deleteStagingDirectory(root);
                    }
                    catch
                    {
                        retained.Add(root);
                    }
                }

                return retained;
            }

            IReadOnlyList<string> CleanupStagingAfterRollback(bool retain)
            {
                if (retain) return stagingRoots.ToArray();
                return CleanupStaging();
            }
        }

        private static IReadOnlyList<string> NormalizeAndValidate(IReadOnlyList<string> paths, bool isDirectory,
            StringComparer comparer)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(comparer);
            foreach (var path in paths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var fullPath = Path.GetFullPath(path);
                if (!seen.Add(fullPath)) continue;

                if (isDirectory)
                {
                    if (!Directory.Exists(fullPath)) continue;
                }
                else if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Delete source does not exist", fullPath);
                }

                result.Add(fullPath);
            }

            return result;
        }

        private static IReadOnlyList<string> Rollback(IReadOnlyList<StagedEntry> staged,
            Action<string, string> moveFile, Action<string, string> moveDirectory)
        {
            var failures = new List<string>();
            for (var i = staged.Count - 1; i >= 0; i--)
            {
                var entry = staged[i];
                try
                {
                    if (File.Exists(entry.Source) || Directory.Exists(entry.Source)) continue;
                    if (entry.IsDirectory)
                    {
                        moveDirectory(entry.Staged, entry.Source);
                    }
                    else
                    {
                        moveFile(entry.Staged, entry.Source);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(entry.Source + ": " + ex.Message);
                }
            }

            return failures;
        }
    }
}
