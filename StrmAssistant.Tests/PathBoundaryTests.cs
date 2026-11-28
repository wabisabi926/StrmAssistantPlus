using System;
using System.IO;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class PathBoundaryTests
    {
        [Fact]
        public void IsSameOrDescendant_AcceptsSamePath()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");

            Assert.True(PathBoundary.IsSameOrDescendant(root, root));
        }

        [Fact]
        public void IsSameOrDescendant_AcceptsChildWithSeparatorBoundary()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");
            var child = Path.Combine(root, "Season 1", "movie.mkv");

            Assert.True(PathBoundary.IsSameOrDescendant(child, root));
        }

        [Fact]
        public void IsSameOrDescendant_RejectsSiblingWithSamePrefix()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");
            var sibling = Path.Combine(Path.GetTempPath(), "media", "Movie2", "movie.mkv");

            Assert.False(PathBoundary.IsSameOrDescendant(sibling, root));
        }

        [Fact]
        public void IsSameOrDescendant_RejectsParentTraversal()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");
            var escaped = Path.Combine(root, "..", "Movie2", "movie.mkv");

            Assert.False(PathBoundary.IsSameOrDescendant(escaped, root));
        }

        [Fact]
        public void IsSameOrDescendant_RejectsInvalidPath()
        {
            Assert.False(PathBoundary.IsSameOrDescendant("not-a-path\u0000", Path.GetTempPath()));
        }

        [Fact]
        public void IsSameOrDescendant_FilesystemRootContainsChild()
        {
            var root = Path.GetPathRoot(Path.GetFullPath("."));
            var child = Path.Combine(root, "media", "movie.mkv");

            Assert.True(PathBoundary.IsSameOrDescendant(child, root));
        }

        [Fact]
        public void IsSameOrDescendant_UsesPlatformCaseSensitivity()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");
            var candidate = Path.Combine(Path.GetTempPath(), "media", "movie", "movie.mkv");

            Assert.Equal(OperatingSystem.IsWindows(), PathBoundary.IsSameOrDescendant(candidate, root));
        }

        [Fact]
        public void IsLocalFileInScope_AcceptsFileInsideRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");
            var child = Path.Combine(root, "movie.mkv");

            Assert.True(PathBoundary.IsLocalFileInScope(child, new[] { root }));
        }

        [Fact]
        public void IsLocalFileInScope_RejectsSiblingAndRemoteUri()
        {
            var root = Path.Combine(Path.GetTempPath(), "media", "Movie");
            var sibling = Path.Combine(Path.GetTempPath(), "media", "Movie2", "movie.mkv");

            Assert.False(PathBoundary.IsLocalFileInScope(sibling, new[] { root }));
            Assert.False(PathBoundary.IsLocalFileInScope("https://example.com/movie.mkv", new[] { root }));
        }

        [Fact]
        public void IsLocalFileInScope_HandlesUncRootOnWindows()
        {
            if (!OperatingSystem.IsWindows()) return;

            const string root = @"\\server\share\Movie";
            const string child = @"\\server\share\Movie\movie.mkv";
            const string sibling = @"\\server\share\Movie2\movie.mkv";

            Assert.True(PathBoundary.IsLocalFileInScope(child, new[] { root }));
            Assert.False(PathBoundary.IsLocalFileInScope(sibling, new[] { root }));
        }

        [Fact]
        public void IsLocalFileInScope_RejectsJunctionOutsideRoot()
        {
            if (!OperatingSystem.IsWindows()) return;

            var sandbox = Path.Combine(Path.GetTempPath(), "strmassistant-boundary-" + Guid.NewGuid().ToString("N"));
            var root = Path.Combine(sandbox, "library");
            var outside = Path.Combine(sandbox, "outside");
            var junction = Path.Combine(root, "linked");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "movie.mkv"), "test");

            try
            {
                var start = new System.Diagnostics.ProcessStartInfo("cmd.exe",
                    $"/c mklink /J \"{junction}\" \"{outside}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var process = System.Diagnostics.Process.Start(start);
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());

                Assert.False(PathBoundary.IsLocalFileInScope(Path.Combine(junction, "movie.mkv"), new[] { root }));
            }
            finally
            {
                if (Directory.Exists(junction)) Directory.Delete(junction);
                if (Directory.Exists(sandbox)) Directory.Delete(sandbox, true);
            }
        }

        [Fact]
        public void IsSameOrDescendant_HandlesUncRootOnWindows()
        {
            if (!OperatingSystem.IsWindows()) return;

            const string root = @"\\server\share\Movie";
            const string child = @"\\server\share\Movie\movie.mkv";
            const string sibling = @"\\server\share\Movie2\movie.mkv";

            Assert.True(PathBoundary.IsSameOrDescendant(child, root));
            Assert.False(PathBoundary.IsSameOrDescendant(sibling, root));
        }
    }
}
