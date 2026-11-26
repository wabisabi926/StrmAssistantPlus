using StrmAssistant.Mod;
using Xunit;

namespace StrmAssistant.Tests
{
    public class PatchHealthTests
    {
        [Theory]
        [InlineData(PatchStatus.Initialized, PatchApproach.Harmony)]
        [InlineData(PatchStatus.Applied, PatchApproach.Harmony)]
        [InlineData(PatchStatus.Initialized, PatchApproach.Reflection)]
        [InlineData(PatchStatus.Applied, PatchApproach.Reflection)]
        public void IsHealthy_AcceptsInitializedOrAppliedWithoutErrors(PatchStatus status, PatchApproach approach)
        {
            var tracker = CreateTracker();
            try
            {
                tracker.Status = status;
                tracker.FallbackPatchApproach = approach;

                Assert.True(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void IsHealthy_RejectsRecordedErrorsEvenWhenFallbackIsReflection()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.Status = PatchStatus.Applied;
                tracker.FallbackPatchApproach = PatchApproach.Reflection;
                tracker.AddError("patch failed");

                Assert.True(tracker.IsRunning);
                Assert.False(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void IsHealthy_RejectsMissingTargetsAndUnsupportedStates()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MissingTargets.Add("target missing");
                Assert.False(tracker.IsHealthy);

                tracker.MissingTargets.Clear();
                tracker.FallbackPatchApproach = PatchApproach.None;
                Assert.False(tracker.IsHealthy);

                tracker.FallbackPatchApproach = PatchApproach.Harmony;
                tracker.Status = PatchStatus.Failed;
                Assert.False(tracker.IsHealthy);

                tracker.Status = PatchStatus.NotSupported;
                Assert.False(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void IsHealthy_DoesNotTreatDisabledFeatureAsPatchFailure()
        {
            var tracker = CreateTracker();
            var optional = CreateTracker(typeof(EnhanceChineseSearch));
            try
            {
                tracker.IsFeatureEnabled = false;

                Assert.True(tracker.IsHealthy);
                Assert.False(PatchManager.IsCoreFeature(optional));
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
                PatchManager.PatchTrackerList.Remove(optional);
            }
        }

        [Fact]
        public void IsCoreFeature_UsesSharedClassification()
        {
            var optional = CreateTracker(typeof(EnhanceChineseSearch));
            var core = CreateTracker(typeof(EnableDeepDelete));
            try
            {
                Assert.False(PatchManager.IsCoreFeature(optional));
                Assert.True(PatchManager.IsCoreFeature(core));
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(optional);
                PatchManager.PatchTrackerList.Remove(core);
            }
        }

        private static PatchTracker CreateTracker(System.Type patchType = null)
        {
            return new PatchTracker(patchType ?? typeof(PatchHealthTests), PatchApproach.Harmony);
        }
    }
}
