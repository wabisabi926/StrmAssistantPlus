using StrmAssistant.Web.Service;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace StrmAssistant.Tests
{
    public class VersionDeleteTransactionTests
    {
        [Fact]
        public void Execute_CommitsAndRemovesStaging()
        {
            var root = CreateTempRoot();
            var source = Path.Combine(root, "movie.mkv");
            File.WriteAllText(source, "media");
            var committed = false;

            var result = VersionDeleteTransaction.Execute(new[] { source }, Array.Empty<string>(),
                File.Move, Directory.Move, () => committed = true, path => Directory.Delete(path, true));

            Assert.True(result.Committed);
            Assert.True(committed);
            Assert.False(File.Exists(source));
            Assert.Empty(result.RetainedStagingPaths);
            Assert.Empty(Directory.GetDirectories(root, ".strmassistant-delete-*"));
        }

        [Fact]
        public void Execute_RollsBackWhenCommitFails()
        {
            var root = CreateTempRoot();
            var source = Path.Combine(root, "movie.mkv");
            File.WriteAllText(source, "media");

            var result = VersionDeleteTransaction.Execute(new[] { source }, Array.Empty<string>(),
                File.Move, Directory.Move, () => throw new IOException("database failure"), path => Directory.Delete(path, true));

            Assert.False(result.Committed);
            Assert.True(File.Exists(source));
            Assert.Equal("media", File.ReadAllText(source));
            Assert.Empty(result.RetainedStagingPaths);
        }

        [Fact]
        public void Execute_RollsBackEarlierFilesWhenLaterMoveFails()
        {
            var root = CreateTempRoot();
            var first = Path.Combine(root, "first.mkv");
            var second = Path.Combine(root, "second.mkv");
            File.WriteAllText(first, "first");
            File.WriteAllText(second, "second");

            void MoveFile(string source, string target)
            {
                if (source == second) throw new IOException("move failure");
                File.Move(source, target);
            }

            var result = VersionDeleteTransaction.Execute(new[] { first, second }, Array.Empty<string>(),
                MoveFile, Directory.Move, () => { }, path => Directory.Delete(path, true));

            Assert.False(result.Committed);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
            Assert.Empty(result.RetainedStagingPaths);
        }

        [Fact]
        public void Execute_RollsBackMetadataDirectoryWhenCommitFails()
        {
            var root = CreateTempRoot();
            var metadata = Path.Combine(root, "metadata");
            Directory.CreateDirectory(metadata);
            File.WriteAllText(Path.Combine(metadata, "nfo"), "metadata");

            var result = VersionDeleteTransaction.Execute(Array.Empty<string>(), new[] { metadata },
                File.Move, Directory.Move, () => throw new IOException("database failure"), path => Directory.Delete(path, true));

            Assert.False(result.Committed);
            Assert.True(Directory.Exists(metadata));
            Assert.True(File.Exists(Path.Combine(metadata, "nfo")));
        }

        [Fact]
        public void Execute_RetainsStagingWhenCleanupFailsAfterCommit()
        {
            var root = CreateTempRoot();
            var source = Path.Combine(root, "movie.mkv");
            File.WriteAllText(source, "media");

            var result = VersionDeleteTransaction.Execute(new[] { source }, Array.Empty<string>(),
                File.Move, Directory.Move, () => { }, _ => throw new IOException("cleanup failure"));

            Assert.True(result.Committed);
            Assert.False(File.Exists(source));
            Assert.Single(result.RetainedStagingPaths);
            Assert.True(Directory.Exists(result.RetainedStagingPaths[0]));
        }

        private static string CreateTempRoot()
        {
            var path = Path.Combine(Path.GetTempPath(), "StrmAssistantDeleteTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
