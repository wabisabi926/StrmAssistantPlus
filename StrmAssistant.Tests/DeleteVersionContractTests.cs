using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class DeleteVersionContractTests
    {
        [Fact]
        public void Preview_DoesNotExposeServerPaths()
        {
            var source = ReadSource("StrmAssistant/Web/Service/LibraryService.cs");

            Assert.DoesNotContain("public string Path { get; set; }", source);
            Assert.DoesNotContain("Path = target.Path", source);
            Assert.DoesNotContain("Path = episode.Path", source);
        }

        [Fact]
        public void Delete_UsesTheSameAuthorizationPlanAsPreview()
        {
            var source = ReadSource("StrmAssistant/Web/Service/LibraryService.cs");

            Assert.Contains("VersionDeletePlanner.Build(item, candidates, CanDeleteItem)", source);
            Assert.Contains("deletePlan.IsDenied || deletePlan.UnauthorizedCount > 0", source);
            Assert.Contains("VersionDeleteTransaction.Execute", source);
            Assert.Contains("PathBoundary.IsLocalFileInScope(p.FullName, deleteScope)", source);
            Assert.DoesNotContain("_fileSystem.DeleteFile", source);
        }

        [Fact]
        public void Delete_ReturnsStructuredStatus()
        {
            var api = ReadSource("StrmAssistant/Web/Api/DeleteVersion.cs");
            var service = ReadSource("StrmAssistant/Web/Service/LibraryService.cs");
            var script = ReadSource("StrmAssistant/Web/Resources/strmassistant.js");

            Assert.Contains("IReturn<DeleteVersionResult>", api);
            Assert.Contains("DeleteVersionResult Any(DeleteVersion request)", service);
            Assert.Contains("\"DryRun\"", service);
            Assert.Contains("\"Denied\"", service);
            Assert.Contains("\"RolledBack\"", service);
            Assert.Contains("result.Status", script);
            Assert.Contains("演练完成，未删除内容", script);
        }

        [Fact]
        public void Frontend_PreviewsBeforeDelete()
        {
            var source = ReadSource("StrmAssistant/Web/Resources/strmassistant.js");

            Assert.Contains("DeleteVersionPreview", source);
            Assert.Contains("preview.WillDelete.length", source);
            Assert.Contains("}).then(postDelete);", source);
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
