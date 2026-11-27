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

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
