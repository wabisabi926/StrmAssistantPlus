using System;
using System.Collections.Generic;
using System.Linq;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class PlaylistSelectionRegressionTests
    {
        private static UdfTitleResolver.PlaylistEntry Entry(int count, bool sameFile, bool sameSegment)
        {
            var entry = new UdfTitleResolver.PlaylistEntry();
            for (var i = 0; i < count; i++)
                entry.Clips.Add(new UdfTitleResolver.ClipEntry
                {
                    Name = sameFile ? "00001" : i.ToString("D5"),
                    InTime = sameSegment ? 10 : 10 + i * 30,
                    Duration = 30
                });
            entry.TotalSeconds = entry.Clips.Sum(c => c.Duration);
            return entry;
        }

        [Fact]
        public void ExcessiveExactReplay_IsRejectedBeforeLongestTitleSelection()
        {
            var entry = Entry(501, true, true);
            entry.Clips[0].Name = "00114";
            for (var i = 1; i < entry.Clips.Count; i++) entry.Clips[i].Name = "00115";
            Assert.Contains("repeated segments", UdfTitleResolver.GetPlaylistRejectionReason(entry));
        }

        [Fact]
        public void OrdinaryMultiPartTitle_IsKept()
        {
            Assert.Null(UdfTitleResolver.GetPlaylistRejectionReason(Entry(15, false, false)));
        }

        [Fact]
        public void ReusedFileWithDifferentInOutTimes_IsKept()
        {
            Assert.Null(UdfTitleResolver.GetPlaylistRejectionReason(Entry(300, true, false)));
        }

        [Fact]
        public void LimitedLegitimateSegmentReplay_IsKept()
        {
            Assert.Null(UdfTitleResolver.GetPlaylistRejectionReason(Entry(2, true, true)));
        }

        [Fact]
        public void InvalidOrEmptyPlaylist_IsRejected()
        {
            Assert.NotNull(UdfTitleResolver.GetPlaylistRejectionReason(new UdfTitleResolver.PlaylistEntry()));
            var entry = Entry(1, true, true);
            entry.Clips[0].Duration = double.NaN;
            Assert.NotNull(UdfTitleResolver.GetPlaylistRejectionReason(entry));
        }

        [Theory]
        [InlineData("http://127.0.0.1/download?filename=disc%2Eiso&sign=a%2Bb%2Fc")]
        [InlineData("http://127.0.0.1/download?path=%2Ffolder%2Fdisc.ISO&sign=a%2Fb")]
        public void IsoFilenameHint_DoesNotRewriteSignedUrl(string url)
        {
            Assert.True(IsoMounter.TryNormalizeRemoteIso(url, out var normalized));
            Assert.Equal(url, normalized);
        }

        [Fact]
        public void AllPlaylistsInvalid_DoesNotFallBackToLargestStream()
        {
            var fs = new FakeDiscFs();
            fs.Files[@"BDMV\PLAYLIST"] = new[] { @"BDMV\PLAYLIST\00000.mpls" };
            fs.Lengths[@"BDMV\PLAYLIST\00000.mpls"] = 4;
            fs.Contents[@"BDMV\PLAYLIST\00000.mpls"] = new byte[4];
            fs.Files[@"BDMV\STREAM"] = new[] { @"BDMV\STREAM\00000.m2ts" };
            fs.Lengths[@"BDMV\STREAM\00000.m2ts"] = 1024;

            var title = UdfTitleResolver.ResolveFromFileSystem(fs, true);

            Assert.NotNull(title);
            Assert.Contains("No reliable playable playlist", title.UnsupportedReason);
            Assert.Null(title.RelativePath);
            Assert.Empty(title.ProbePaths);
        }

        [Fact]
        public void NoPlaylist_StillUsesLargestStreamFallback()
        {
            var fs = new FakeDiscFs();
            fs.Files[@"BDMV\PLAYLIST"] = Array.Empty<string>();
            fs.Files[@"BDMV\STREAM"] = new[] { @"BDMV\STREAM\00000.m2ts", @"BDMV\STREAM\00001.m2ts" };
            fs.Lengths[@"BDMV\STREAM\00000.m2ts"] = 1024;
            fs.Lengths[@"BDMV\STREAM\00001.m2ts"] = 2048;

            var title = UdfTitleResolver.ResolveFromFileSystem(fs, true);

            Assert.NotNull(title);
            Assert.Null(title.UnsupportedReason);
            Assert.Equal(@"BDMV\STREAM\00001.m2ts", title.RelativePath);
        }

        private sealed class FakeDiscFs : UdfTitleResolver.IDiscFs
        {
            public Dictionary<string, string[]> Files { get; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, long> Lengths { get; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, byte[]> Contents { get; } = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            public bool DirectoryExists(string relativeDir) =>
                Files.ContainsKey(relativeDir) || Lengths.Keys.Any(path => path.StartsWith(relativeDir + "\\", StringComparison.OrdinalIgnoreCase));

            public bool FileExists(string relativePath) => Lengths.ContainsKey(relativePath);

            public string[] GetFiles(string relativeDir) =>
                Files.TryGetValue(relativeDir, out var files) ? files : Array.Empty<string>();

            public long GetFileLength(string relativePath) => Lengths.TryGetValue(relativePath, out var length) ? length : 0;

            public byte[] ReadAllBytes(string relativePath) =>
                Contents.TryGetValue(relativePath, out var content) ? content : Array.Empty<byte>();
        }
    }
}
