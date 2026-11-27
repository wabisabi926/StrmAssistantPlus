using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class PerformanceContractTests
    {
        [Fact]
        public void PreExtractQuery_DoesNotRepeatFullLibraryQueryForImages()
        {
            var source = ReadSource("StrmAssistant/Common/LibraryApi.cs");

            Assert.Contains("var candidates = _libraryManager.GetItemList(candidateQuery);", source);
            Assert.DoesNotContain("var itemsImageCaptureQuery = new InternalItemsQuery", source);
        }

        [Fact]
        public void ForceReExtractFile_UsesAtomicReplacementAndBatchedWrites()
        {
            var source = ReadSource("StrmAssistant/Common/LibraryApi.cs");

            Assert.Contains("AtomicFileReplacer.Replace(tempPath, path)", source);
            Assert.Contains("_forceReExtractPendingWrites < 64", source);
            Assert.Contains("public static void FlushForceReExtract()", source);
        }

        [Fact]
        public void UdfResolver_BoundsPlaylistMetadataReads()
        {
            var source = ReadSource("StrmAssistant/Common/UdfTitleResolver.cs");

            Assert.Contains("MaxPlaylistMetadataBytes", source);
            Assert.Contains("MaxPlaylistFiles", source);
            Assert.Contains("metadataLength > MaxPlaylistMetadataBytes", source);
        }

        [Fact]
        public void DirectRedirect_IsAsyncAndReusesConnections()
        {
            var source = ReadSource("StrmAssistant/Mod/StrmDirectRedirect.cs");

            Assert.Contains("ResolveRedirectUrlAsync", source);
            Assert.DoesNotContain("}, method).GetAwaiter().GetResult();", source);
            Assert.Contains("EnableKeepAlive = true", source);
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
