using StrmAssistant.Core;
using StrmAssistant.Mod;
using Xunit;

namespace StrmAssistant.Tests
{
    public class ChineseSearchCompatibilityTests
    {
        [Theory]
        [InlineData("\"多段300\"*", "多段300")]
        [InlineData("^\"死侍與金剛狼\"*$", "死侍與金剛狼")]
        [InlineData("\"deadpool\" \"wolverine\"*", "deadpool wolverine")]
        [InlineData("duoduan", "duoduan")]
        [InlineData(null, null)]
        public void StripFtsExpressionWrappers_RemovesOnlyFtsSyntax(string input, string expected)
        {
            Assert.Equal(expected, EnhanceChineseSearch.StripFtsExpressionWrappers(input));
        }

        [Fact]
        public void ResolveByNameOrSignature_FindsTwoArgumentCreateSearchTerm()
        {
            var target = EmbyTargetResolver.ResolveByNameOrSignature(
                typeof(ChineseSearchCompatibilityTests),
                nameof(CreateSearchTerm),
                new[] { typeof(string), typeof(bool) },
                typeof(string),
                true);

            Assert.True(target.IsFound);
            Assert.Equal(2, target.Method.GetParameters().Length);
        }

        [Fact]
        public void SimplifiedSweepFailure_IsReportedAsAlternativeAndRecoversOnSuccess()
        {
            var tracker = new PatchTracker(typeof(EnhanceChineseSearch), PatchApproach.Harmony);
            try
            {
                EnhanceChineseSearch.SetSimplifiedSweepStatus(tracker, false);

                Assert.Equal(PatchStatus.AlternativeActive, tracker.Status);
                Assert.Equal(EnhanceChineseSearch.SimplifiedSweepDegradedReason, tracker.AlternativeReason);
                Assert.True(tracker.IsHealthy);

                EnhanceChineseSearch.SetSimplifiedSweepStatus(tracker, true);

                Assert.Equal(PatchStatus.Applied, tracker.Status);
                Assert.Null(tracker.AlternativeReason);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void SimplifiedSweepSuccess_DoesNotClearOtherAlternativeReason()
        {
            var tracker = new PatchTracker(typeof(EnhanceChineseSearch), PatchApproach.Harmony);
            try
            {
                tracker.MarkAlternativeActive("another active alternative");
                EnhanceChineseSearch.SetSimplifiedSweepStatus(tracker, true);

                Assert.Equal(PatchStatus.AlternativeActive, tracker.Status);
                Assert.Equal("another active alternative", tracker.AlternativeReason);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        private static string CreateSearchTerm(string searchTerm, bool matchAnyWord) => searchTerm;
    }
}
