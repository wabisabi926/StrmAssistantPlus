using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class CancellationContractTests
    {
        [Fact]
        public void QueueManager_DoesNotSchedulePermitHoldingDelegatesWithCancellationToken()
        {
            var source = ReadSource("StrmAssistant/Common/QueueManager.cs");

            Assert.DoesNotContain("}, cancellationToken);", source);
            Assert.Contains("}, CancellationToken.None);", source);
        }

        [Fact]
        public void SubtitleApi_PropagatesCancellationToProbeAndPersistence()
        {
            var source = ReadSource("StrmAssistant/Common/SubtitleApi.cs");

            Assert.Contains("bool persistMediaInfo, CancellationToken cancellationToken", source);
            Assert.Contains("UpdateExternalSubtitleStream(item, subtitleStream, refreshOptions, cancellationToken)", source);
            Assert.Contains("SaveMediaStreams(item.InternalId, currentStreams, cancellationToken)", source);
        }

        [Fact]
        public void SubtitleTask_PassesTaskTokenAndAvoidsBlockingWait()
        {
            var source = ReadSource("StrmAssistant/ScheduledTask/ScanExternalSubtitleTask.cs");

            Assert.Contains("UpdateExternalSubtitles(taskItem, refreshOptions, false, persistMediaInfo, cancellationToken)", source);
            Assert.DoesNotContain("Task.Delay(10).Wait()", source);
        }

        [Fact]
        public void BatchTasks_DrainStartedWorkersBeforeCompleting()
        {
            var files = new[]
            {
                "StrmAssistant/ScheduledTask/ExtractMediaInfoTask.cs",
                "StrmAssistant/ScheduledTask/ScanExternalSubtitleTask.cs",
                "StrmAssistant/ScheduledTask/PersistMediaInfoTask.cs",
                "StrmAssistant/ScheduledTask/RefreshEpisodeTask.cs",
                "StrmAssistant/ScheduledTask/NormalizeTrackLanguageTask.cs",
                "StrmAssistant/ScheduledTask/ExtractVideoThumbnailTask.cs",
                "StrmAssistant/ScheduledTask/RefreshPersonTask.cs",
                "StrmAssistant/ScheduledTask/ExtractIntroFingerprintTask.cs"
            };

            foreach (var file in files)
            {
                var source = ReadSource(file);
                Assert.Contains("finally", source);
                Assert.Contains("await Task.WhenAll(", source);
            }
        }

        [Fact]
        public void ForceReExtract_IsClearedOnlyAfterSuccess()
        {
            var extract = ReadSource("StrmAssistant/ScheduledTask/ExtractMediaInfoTask.cs");
            var catchup = ReadSource("StrmAssistant/Common/LibraryApi.cs");

            Assert.Contains("result == true && LibraryApi.IsForceReExtract", extract);
            Assert.Contains("result == true && LibraryApi.IsForceReExtract", catchup);
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
