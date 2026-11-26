using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class MediaInfoFeedbackContractTests
    {
        [Fact]
        public void DeleteResponse_DoesNotReturnGenericOkMessage()
        {
            var source = ReadSource("StrmAssistant/Web/Service/ItemService.cs");

            Assert.DoesNotContain("response.Message = \"ok\";", source);
        }

        [Fact]
        public void Frontend_ShowsDeleteCountsWithoutServerSummary()
        {
            var source = ReadSource("StrmAssistant/Web/Resources/strmassistant.js");

            Assert.Contains("const failed =", source);
            Assert.Contains("Deleted: ${succeeded", source);
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
