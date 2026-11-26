using StrmAssistant.Mod;
using StrmAssistant.Web.Service;
using System;
using System.Collections.Generic;
using System.Reflection;
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

        [Theory]
        [InlineData(PatchStatus.NotInitialized)]
        [InlineData(PatchStatus.Initializing)]
        public void IsHealthy_RejectsUninitializedStates(PatchStatus status)
        {
            var tracker = CreateTracker();
            try
            {
                tracker.Status = status;

                Assert.False(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void IsHealthy_DisabledUninitializedFeatureIsSkippedNotFailed()
        {
            var tracker = CreateTracker();
            var optional = CreateTracker(typeof(EnhanceChineseSearch));
            try
            {
                tracker.IsFeatureEnabled = false;
                tracker.Status = PatchStatus.Failed;

                Assert.False(tracker.IsHealthy);
                Assert.False(PatchManager.IsCoreFeature(optional));

                var summary = BuildSummary(new List<PatchTracker> { tracker });
                Assert.Equal(0, summary.Failed);
                Assert.Equal(1, summary.Skipped);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
                PatchManager.PatchTrackerList.Remove(optional);
            }
        }

        [Fact]
        public void BuildSummary_DoesNotCountDisabledUnhealthyPatchAsFailed()
        {
            var enabled = CreateTracker();
            var disabled = CreateTracker();
            try
            {
                enabled.Status = PatchStatus.Failed;
                disabled.IsFeatureEnabled = false;
                disabled.Status = PatchStatus.Failed;

                var summary = BuildSummary(new List<PatchTracker> { enabled, disabled });

                Assert.Equal(1, summary.Failed);
                Assert.Equal(1, summary.Skipped);
                Assert.Equal(1, summary.Running);
                Assert.Equal(0, summary.Healthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(enabled);
                PatchManager.PatchTrackerList.Remove(disabled);
            }
        }

        [Fact]
        public void MarkInitialized_CompletesLifecycle()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkInitialized();

                Assert.Equal(PatchStatus.Initialized, tracker.Status);
                Assert.NotNull(tracker.InitializedAt);
                Assert.True(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void MarkUnsupported_IsExcludedFromSupportedSummary()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkUnsupported();

                Assert.Equal(PatchStatus.NotSupported, tracker.Status);
                Assert.False(tracker.IsSupported);
                Assert.False(tracker.IsHealthy);

                var summary = BuildSummary(new List<PatchTracker> { tracker });
                Assert.Equal(0, summary.Supported);
                Assert.Equal(1, summary.Unsupported);
                Assert.Equal(0, summary.Failed);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void MarkFailed_IsCountedAsFailed()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkFailed("required component missing");

                Assert.Equal(PatchStatus.Failed, tracker.Status);
                Assert.True(tracker.HasErrors);
                Assert.False(tracker.IsHealthy);

                var summary = BuildSummary(new List<PatchTracker> { tracker });
                Assert.Equal(1, summary.Supported);
                Assert.Equal(1, summary.Failed);
                Assert.Equal(0, summary.Unsupported);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void MarkComponentsUnavailable_Emby411IsUnsupported()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkComponentsUnavailable("known limitation", StrmAssistant.Core.EmbyApiVersion.V4_11);

                Assert.Equal(PatchStatus.NotSupported, tracker.Status);
                Assert.False(tracker.IsSupported);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void MarkComponentsUnavailable_Emby410IsFailed()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkComponentsUnavailable("unexpected failure", StrmAssistant.Core.EmbyApiVersion.V4_10);

                Assert.Equal(PatchStatus.Failed, tracker.Status);
                Assert.True(tracker.IsSupported);
                Assert.True(tracker.HasErrors);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void MarkInitializationFailed_RemainsFailedWithErrors()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkInitializationFailed("resolver exploded");

                Assert.Equal(PatchStatus.Failed, tracker.Status);
                Assert.Equal(PatchApproach.None, tracker.FallbackPatchApproach);
                Assert.True(tracker.HasErrors);
                Assert.False(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void MarkAlternativeActive_IsHealthyAndPreservesReason()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkAlternativeActive("native transaction + snapshot restore");

                Assert.Equal(PatchStatus.AlternativeActive, tracker.Status);
                Assert.Equal("native transaction + snapshot restore", tracker.AlternativeReason);
                Assert.True(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void CompletePatch_PreservesAlternativeActive()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.MarkAlternativeActive("setter-based DTO filter");

                tracker.CompletePatch();

                Assert.Equal(PatchStatus.AlternativeActive, tracker.Status);
                Assert.True(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void CompletePatch_MarksNormalSuccessApplied()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.Status = PatchStatus.Initialized;

                tracker.CompletePatch();

                Assert.Equal(PatchStatus.Applied, tracker.Status);
                Assert.True(tracker.IsHealthy);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
            }
        }

        [Fact]
        public void BuildSummary_TreatsNotSupportedStatusAsUnsupported()
        {
            var tracker = CreateTracker();
            try
            {
                tracker.Status = PatchStatus.NotSupported;

                var summary = BuildSummary(new List<PatchTracker> { tracker });
                Assert.Equal(0, summary.Supported);
                Assert.Equal(1, summary.Unsupported);
                Assert.Equal(0, summary.Failed);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(tracker);
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

        [Fact]
        public void Patch_PreservesInitializationFailure()
        {
            var patch = new NoopPatch();
            try
            {
                patch.PatchTracker.MarkInitializationFailed("resolver exploded");

                patch.Patch();

                Assert.Equal(PatchStatus.Failed, patch.PatchTracker.Status);
                Assert.True(patch.PatchTracker.HasErrors);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(patch.PatchTracker);
            }
        }

        [Fact]
        public void Patch_PrepareExceptionBecomesFailed()
        {
            var patch = new ThrowingPreparePatch();
            try
            {
                patch.Patch();

                Assert.Equal(PatchStatus.Failed, patch.PatchTracker.Status);
                Assert.True(patch.PatchTracker.HasErrors);
            }
            finally
            {
                PatchManager.PatchTrackerList.Remove(patch.PatchTracker);
            }
        }

        private sealed class NoopPatch : PatchBase<NoopPatch>
        {
            protected override void OnInitialize()
            {
            }

            protected override void Prepare(bool apply)
            {
            }
        }

        private sealed class ThrowingPreparePatch : PatchBase<ThrowingPreparePatch>
        {
            protected override void OnInitialize()
            {
            }

            protected override void Prepare(bool apply)
            {
                throw new InvalidOperationException("prepare exploded");
            }
        }

        private static DiagnosticService.PatchSummary BuildSummary(List<PatchTracker> trackers)
        {
            var method = typeof(DiagnosticService).GetMethod("BuildSummary",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (DiagnosticService.PatchSummary)method.Invoke(null, new object[] { trackers });
        }

        private static PatchTracker CreateTracker(System.Type patchType = null)
        {
            return new PatchTracker(patchType ?? typeof(PatchHealthTests), PatchApproach.Harmony);
        }
    }
}
