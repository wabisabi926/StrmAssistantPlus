using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class LifecycleContractTests
    {
        [Fact]
        public void Plugin_UnsubscribesEveryEventAndShutsDownSubsystems()
        {
            var source = ReadSource("StrmAssistant/Plugin.cs");

            Assert.Contains("BackgroundTaskRunner.BeginShutdown()", source);
            Assert.Contains("UnsubscribeEvents()", source);
            Assert.Contains("QueueManager.ShutdownAsync", source);
            Assert.Contains("PatchManager.Shutdown()", source);
            Assert.Contains("_libraryManager.ItemAdded -= OnItemAdded", source);
            Assert.Contains("_libraryManager.ItemUpdated -= OnItemUpdated", source);
            Assert.Contains("_libraryManager.ItemRemoved -= OnItemRemoved", source);
            Assert.Contains("_providerManager.RefreshCompleted -= OnRefreshCompleted", source);
            Assert.Contains("_sessionManager.PlaybackStart -= OnPlaybackStart", source);
            Assert.Contains("_sessionManager.PlaybackStopped -= OnPlaybackStopped", source);
            Assert.Contains("_userManager.UserCreated -= OnUserCreated", source);
            Assert.Contains("_userManager.UserDeleted -= OnUserDeleted", source);
            Assert.Contains("_userManager.UserConfigurationUpdated -= OnUserConfigurationUpdated", source);
            Assert.Contains("_userDataManager.UserDataSaved -= OnUserDataSaved", source);
            Assert.Contains("CollectionFolder.LibraryOptionsUpdated -= OnLibraryOptionsUpdated", source);
        }

        [Fact]
        public void PostScanTask_ChecksRuntimeStateBeforeUsingServices()
        {
            var source = ReadSource("StrmAssistant/ScheduledTask/TriggerPopulateDoubanIdTask.cs");

            Assert.Contains("Plugin.IsPluginEnabled", source);
            Assert.Contains("Plugin.IsShuttingDown", source);
            Assert.Contains("Plugin.DoubanAssist == null", source);
        }

        [Fact]
        public void OptionStores_SkipRuntimeSideEffectsWhenInactive()
        {
            var files = new[]
            {
                "StrmAssistant/Options/Store/PluginOptionsStore.cs",
                "StrmAssistant/Options/Store/MetadataEnhanceOptionsStore.cs",
                "StrmAssistant/Options/Store/MediaInfoExtractOptionsStore.cs",
                "StrmAssistant/Options/Store/IntroSkipOptionsStore.cs",
                "StrmAssistant/Options/Store/ExperienceEnhanceOptionsStore.cs",
                "StrmAssistant/Options/Store/NetworkProxyOptionsStore.cs"
            };

            foreach (var file in files)
            {
                Assert.Contains("if (!RuntimeActive) return;", ReadSource(file));
            }
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
