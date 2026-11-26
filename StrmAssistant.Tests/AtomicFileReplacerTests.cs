using System;
using System.IO;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class AtomicFileReplacerTests
    {
        [Fact]
        public void Replace_ReplacesDestinationAndRemovesSource()
        {
            var directory = CreateTempDirectory();
            var source = Path.Combine(directory, "image.crop.jpg");
            var destination = Path.Combine(directory, "image.jpg");
            try
            {
                File.WriteAllText(source, "new");
                File.WriteAllText(destination, "old");

                AtomicFileReplacer.Replace(source, destination);

                Assert.Equal("new", File.ReadAllText(destination));
                Assert.False(File.Exists(source));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Replace_WhenReplacementThrows_PreservesDestinationAndSource()
        {
            var directory = CreateTempDirectory();
            var source = Path.Combine(directory, "image.crop.jpg");
            var destination = Path.Combine(directory, "image.jpg");
            try
            {
                File.WriteAllText(source, "new");
                File.WriteAllText(destination, "old");

                Assert.Throws<IOException>(() => AtomicFileReplacer.Replace(source, destination,
                    (_, __) => throw new IOException("replace failed")));

                Assert.Equal("old", File.ReadAllText(destination));
                Assert.Equal("new", File.ReadAllText(source));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Replace_WhenDestinationIsLocked_PreservesOriginal()
        {
            if (!OperatingSystem.IsWindows()) return;

            var directory = CreateTempDirectory();
            var source = Path.Combine(directory, "image.crop.jpg");
            var destination = Path.Combine(directory, "image.jpg");
            try
            {
                File.WriteAllText(source, "new");
                File.WriteAllText(destination, "old");

                using (var stream = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var error = Record.Exception(() => AtomicFileReplacer.Replace(source, destination));
                    Assert.True(error is IOException || error is UnauthorizedAccessException);
                }

                Assert.Equal("old", File.ReadAllText(destination));
                Assert.Equal("new", File.ReadAllText(source));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Replace_WhenDestinationMissing_MovesSource()
        {
            var directory = CreateTempDirectory();
            var source = Path.Combine(directory, "image.crop.jpg");
            var destination = Path.Combine(directory, "image.jpg");
            try
            {
                File.WriteAllText(source, "new");

                AtomicFileReplacer.Replace(source, destination);

                Assert.Equal("new", File.ReadAllText(destination));
                Assert.False(File.Exists(source));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "strmassistant-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
