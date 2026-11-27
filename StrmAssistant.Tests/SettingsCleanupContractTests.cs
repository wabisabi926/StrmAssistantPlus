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
        public void SettingsSearch_RestoresOriginalDisplayAndIsPluginScoped()
        {
            var source = ReadSource("StrmAssistant/Web/Resources/strmassistant.js");

            Assert.Contains("originalDisplays", source);
            Assert.Contains("restoreDisplays", source);
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
