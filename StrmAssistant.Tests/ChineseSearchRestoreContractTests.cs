using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class ChineseSearchRestoreContractTests
    {
        [Fact]
        public void DisableForIndexRestore_DoesNotClearPendingRestore()
        {
            var source = ReadSource("StrmAssistant/Plugin.cs");
            var start = source.IndexOf("void DisableForIndexRestore", StringComparison.Ordinal);
            var end = source.IndexOf("MainOptionsStore.SavePluginOptionsSuppress", start, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);

            var body = source.Substring(start, end - start);
            Assert.DoesNotContain("EnhanceChineseSearchRestore = false", body);
            Assert.Contains("index restore remains queued", body);
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
