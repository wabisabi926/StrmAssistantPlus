using Emby.Web.GenericEdit.Elements.List;
using StrmAssistant.Options;
using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class SettingsCleanupContractTests
    {
        [Fact]
        public void IntroDbCache_HasCapacityAndExpiryCleanup()
        {
            var source = ReadSource("StrmAssistant/Common/IntroDbApi.cs");

            Assert.Contains("MaxCacheEntries", source);
            Assert.Contains("OrderBy(p => p.Value.Expiry)", source);
        }

        [Fact]
        public void PatchLifecycle_SynchronizesFeatureFlag()
        {
            var source = ReadSource("StrmAssistant/Mod/PatchBase.cs");

            Assert.Contains("PatchTracker.IsFeatureEnabled = true", source);
            Assert.Contains("PatchTracker.IsFeatureEnabled = false", source);
        }

        [Fact]
        public void ChapterSerialization_UsesTrackedBackgroundTasks()
        {
            var source = ReadSource("StrmAssistant/Mod/ChapterChangeTracker.cs");

            Assert.Contains("BackgroundTaskRunner.Run", source);
            Assert.DoesNotContain("ConfigureAwait(false)", source);
        }

        [Fact]
        public void SettingsConditions_DependOnMasterSwitches()
        {
            var hdr = ReadSource("StrmAssistant/Options/HdrTaggingOptions.cs");
            var playback = ReadSource("StrmAssistant/Options/ExperiencePlaybackOptions.cs");
            var network = ReadSource("StrmAssistant/Options/NetworkOptions.cs");

            Assert.Contains("[EnabledCondition(nameof(EnableHdrTagging), SimpleCondition.IsTrue)]", hdr);
            Assert.Contains("[MinValue(5), MaxValue(100)]", playback);
            Assert.DoesNotContain("ProxyServerStatus.StatusText = string.Empty", network);
        }

        [Fact]
        public void SettingsConditions_FollowSourceAndEmbyVersion()
        {
            var community = ReadSource("StrmAssistant/Options/CommunityRatingOptions.cs");
            var experience = ReadSource("StrmAssistant/Options/ExperienceEnhanceOptions.cs");
            var intro = ReadSource("StrmAssistant/Options/IntroSkipOptions.cs");
            var fingerprint = ReadSource("StrmAssistant/Options/IntroSkipFingerprintOptions.cs");

            Assert.Contains("[VisibleCondition(nameof(IsTmdbSource), SimpleCondition.IsTrue)]", community);
            Assert.Contains("[VisibleCondition(nameof(HasCommunityRatingSource), SimpleCondition.IsTrue)]", community);
            Assert.Contains("Resources.ExperienceEnhanceOptions_Group_Logs", experience);
            Assert.Contains("Resources.IntroSkipOptions_Group_IntroCreditsDetection", intro);
            Assert.Contains("Resources.IntroSkipOptions_UnlockIntroSkip_Intro_Credits_Enhanced", intro);
            Assert.Contains("nameof(ShowParallelMarkerComputation)", fingerprint);
            Assert.Contains("GetCurrentApiVersion() < EmbyApiVersion.V4_11", fingerprint);
        }

        [Fact]
        public void LegacyFingerprintTask_IsHiddenOnEmby411()
        {
            var source = ReadSource("StrmAssistant/ScheduledTask/ExtractIntroFingerprintTask.cs");

            Assert.Contains("public bool IsHidden => EmbyVersionAdapter.GetCurrentApiVersion() >= EmbyApiVersion.V4_11;", source);
        }

        [Fact]
        public void DynamicResultLists_RequireItemsBeforeShowing()
        {
            var merge = new VersionMergeOptions { MergeMultiVersion = true };
            Assert.False(merge.HasSplitMoviesProgress);
            merge.SplitMoviesProgress.Add(new GenericListItem());
            Assert.True(merge.HasSplitMoviesProgress);

            var playback = new IntroSkipPlaybackOptions();
            Assert.False(playback.HasClearIntroResult);
            playback.ClearIntroResult.Add(new GenericListItem());
            Assert.True(playback.HasClearIntroResult);

            var fingerprint = new IntroSkipFingerprintOptions { UnlockIntroSkip = true };
            Assert.False(fingerprint.HasFingerprintBlacklistShowsResult);
            fingerprint.FingerprintBlacklistShowsResult.Add(new GenericListItem());
            Assert.True(fingerprint.HasFingerprintBlacklistShowsResult);
        }

        [Fact]
        public void SettingsSearch_RestoresOriginalDisplayAndIsPluginScoped()
        {
            var source = ReadSource("StrmAssistant/Web/Resources/strmassistant.js");

            Assert.Contains("originalDisplays = new Map()", source);
            Assert.DoesNotContain("originalDisplays = new WeakMap()", source);
            Assert.Contains("restoreDisplays", source);
            Assert.Contains("removeEmptySubCaptions", source);
            Assert.Contains("document.querySelectorAll('.mainContentParent')", source);
            Assert.Contains("section.classList.contains('verticalSection')", source);
            Assert.Contains("hash.indexOf('genericui')", source);
            Assert.Contains("text.indexOf('strmassistant')", source);
            Assert.DoesNotContain("el.style.display = '';", source);
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
