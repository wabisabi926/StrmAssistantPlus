using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emby.Web.GenericEdit;
using StrmAssistant.Tests.Support;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    /// 回归：配置文件不存在时，GetOptions() 必须返回**同一个**实例。
    ///
    /// 旧实现里 ReloadOptions() 在「文件不存在」分支只 return 不赋值，
    /// 导致 this.options 始终为 null，每次调用都 new 一个新对象。
    /// 后果是「读-改-写」分两次取对象的代码会改在临时对象上，
    /// 落盘的仍是全默认值（Plugin.cs 里 NoBoxsetsAutoCreation 回写就是这种情况）。
    /// </summary>
    public class SimpleFileStoreTests
    {
        public class SampleOptions : EditableOptionsBase
        {
            public override string EditorTitle => "Sample";

            public string Value { get; set; }

            public bool Flag { get; set; }
        }

        [Fact]
        public void GetOptions_WithoutFile_ReturnsSameInstance()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");

            var first = store.GetOptions();
            var second = store.GetOptions();

            Assert.Same(first, second);
        }

        [Fact]
        public void ReloadOptions_WithoutFile_ReturnsSameInstanceAsGetOptions()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");

            var fromGet = store.GetOptions();
            var fromReload = store.ReloadOptions();

            Assert.Same(fromGet, fromReload);
        }

        [Fact]
        public void GetOptions_WithoutFile_DoesNotCreateFile()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");

            store.GetOptions();

            Assert.False(System.IO.File.Exists(fixture.FilePath("TestPlugin")));
        }

        /// <summary>
        /// 复现真实 bug：没有配置文件时，先改再存——改的那份必须被保存下来。
        /// 旧实现在此用例下会写出全默认值。
        /// </summary>
        [Fact]
        public void MutateThenSave_WithoutFile_PersistsMutation()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");

            store.GetOptions().Value = "changed";
            store.SetOptions(store.GetOptions());

            var reread = fixture.Create<SampleOptions>("TestPlugin").GetOptions();
            Assert.Equal("changed", reread.Value);
        }

        [Fact]
        public void SetOptions_ThenGetOptions_ReturnsSameInstance()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");

            var options = new SampleOptions { Value = "v1", Flag = true };
            store.SetOptions(options);

            Assert.Same(options, store.GetOptions());
            Assert.True(System.IO.File.Exists(fixture.FilePath("TestPlugin")));
        }

        [Fact]
        public void SetOptions_RaisesFileSavedOnlyAfterAtomicReplacement()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");
            store.SetOptions(new SampleOptions { Value = "old" });
            var events = new List<string>();

            store.FileSaving += (_, _) =>
            {
                events.Add("saving");
                Assert.Equal("old", fixture.Create<SampleOptions>("TestPlugin").GetOptions().Value);
            };
            store.FileSaved += (_, _) =>
            {
                events.Add("saved");
                Assert.Equal("new", fixture.Create<SampleOptions>("TestPlugin").GetOptions().Value);
            };

            var options = new SampleOptions { Value = "new" };
            store.SetOptions(options);

            Assert.Equal(new[] { "saving", "saved" }, events);
            Assert.Same(options, store.GetOptions());
        }

        [Fact]
        public void SetOptions_WhenWriteFails_DoesNotRaiseFileSaved()
        {
            using var fixture = new SimpleStoreFixture();
            var path = fixture.FilePath("TestPlugin");
            Directory.CreateDirectory(path);
            var store = fixture.Create<SampleOptions>("TestPlugin");
            var savingCount = 0;
            var savedCount = 0;
            store.FileSaving += (_, _) => savingCount++;
            store.FileSaved += (_, _) => savedCount++;

            Assert.Throws<IOException>(() => store.SetOptions(new SampleOptions { Value = "not-saved" }));

            Assert.Equal(1, savingCount);
            Assert.Equal(0, savedCount);
            Assert.Empty(Directory.GetFiles(fixture.ConfigDir, "*.tmp-*"));
        }

        [Fact]
        public void SetOptions_WhenSavingIsCanceled_DoesNotWriteOrRaiseFileSaved()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");
            var savedCount = 0;
            store.FileSaving += (_, e) => e.Cancel = true;
            store.FileSaved += (_, _) => savedCount++;

            store.SetOptions(new SampleOptions { Value = "canceled" });

            Assert.Equal(0, savedCount);
            Assert.False(File.Exists(fixture.FilePath("TestPlugin")));
            Assert.Empty(Directory.GetFiles(fixture.ConfigDir, "*.tmp-*"));
        }

        [Fact]
        public void CorruptFile_IsPreservedAndCannotBeOverwrittenAutomatically()
        {
            using var fixture = new SimpleStoreFixture();
            var path = fixture.FilePath("TestPlugin");
            File.WriteAllText(path, "not-json");
            var store = fixture.Create<SampleOptions>("TestPlugin");
            var savingCount = 0;
            var savedCount = 0;
            store.FileSaving += (_, _) => savingCount++;
            store.FileSaved += (_, _) => savedCount++;

            var options = store.GetOptions();
            Assert.NotNull(options);
            Assert.Equal("not-json", File.ReadAllText(path));
            Assert.Throws<InvalidOperationException>(() => store.SetOptions(new SampleOptions { Value = "overwrite" }));
            Assert.Equal(0, savingCount);
            Assert.Equal(0, savedCount);
            Assert.Equal("not-json", File.ReadAllText(path));
        }

        [Fact]
        public void SetOptions_UsesAtomicReplacementAndLeavesNoTempFile()
        {
            using var fixture = new SimpleStoreFixture();
            var store = fixture.Create<SampleOptions>("TestPlugin");

            store.SetOptions(new SampleOptions { Value = "atomic", Flag = true });

            Assert.Equal("atomic", fixture.Create<SampleOptions>("TestPlugin").GetOptions().Value);
            Assert.Empty(Directory.GetFiles(fixture.ConfigDir, "*.tmp-*"));
        }

        [Fact]
        public void GetOptions_WhenFileExists_LoadsValuesAndCachesInstance()
        {
            using var fixture = new SimpleStoreFixture();

            fixture.Create<SampleOptions>("TestPlugin")
                .SetOptions(new SampleOptions { Value = "persisted", Flag = true });

            var store = fixture.Create<SampleOptions>("TestPlugin");
            var first = store.GetOptions();
            var second = store.GetOptions();

            Assert.Same(first, second);
            Assert.Equal("persisted", first.Value);
            Assert.True(first.Flag);
        }

        [Fact]
        public void OptionStores_DeferRuntimeSideEffectsUntilFileSaved()
        {
            var files = new[]
            {
                "StrmAssistant/Options/Store/PluginOptionsStore.cs",
                "StrmAssistant/Options/Store/MediaInfoExtractOptionsStore.cs",
                "StrmAssistant/Options/Store/IntroSkipOptionsStore.cs",
                "StrmAssistant/Options/Store/ExperienceEnhanceOptionsStore.cs",
                "StrmAssistant/Options/Store/MetadataEnhanceOptionsStore.cs",
                "StrmAssistant/Options/Store/NetworkProxyOptionsStore.cs"
            };
            var runtimeSideEffects = new[]
            {
                ".Patch(", ".Unpatch(", "QueueManager.Initialize(", "QueueManager.Dispose(",
                "NotifyPendingRestart(", "ApplySettings(", "UpdateLibraryPathsInScope(",
                "ShortcutMenuHelper.Refresh(", "ClearCache(", "ApplyOptionsAndConfigure(",
                "LocalMetadataCache.", "FfmpegPathOverride.Apply(", "DismountAll(",
                "UpdateResourcePool(", "PatchTimeout(", "ScheduleIndexRebuild(",
                "UpdateMasterSemaphore(", "UpdateTier2Semaphore(", "UpdateCatchupScope(",
                "UpdateSearchScope(", "UpdateIntroSkipPreferences(", "UpdateExclusiveControlFeatures(",
                "UpdateUsersInScope(", "UpdateClientInScope(", "UpdateFolderGroupingLimit(",
                "UpdateLibraryIntroDetectionFingerprintLength(",
                "EnsureLibraryEnabledAutomaticSeriesGrouping("
            };

            foreach (var file in files)
            {
                var source = ReadSource(file);
                var savingBody = ExtractMethodBody(source, "private void OnFileSaving");
                var savedBody = ExtractMethodBody(source, "private void OnFileSaved");

                Assert.Contains("private readonly HashSet<string> _pendingChangedProperties", source);
                Assert.Contains("PropertyChangeDetector.DetectObjectPropertyChanges", savingBody);
                Assert.Contains("_pendingChangedProperties.UnionWith", savingBody);
                Assert.Contains("var changedProperties = new HashSet<string>(_pendingChangedProperties);", savedBody);

                var clearIndex = savedBody.IndexOf("_pendingChangedProperties.Clear();", StringComparison.Ordinal);
                Assert.True(clearIndex >= 0, file);
                Assert.True(runtimeSideEffects.All(token => !savingBody.Contains(token, StringComparison.Ordinal)), file);

                var savedSideEffectIndexes = runtimeSideEffects
                    .Select(token => savedBody.IndexOf(token, StringComparison.Ordinal))
                    .Where(index => index >= 0)
                    .ToArray();
                Assert.NotEmpty(savedSideEffectIndexes);
                Assert.All(savedSideEffectIndexes, index => Assert.True(index > clearIndex, file));
            }
        }

        private static string ExtractMethodBody(string source, string methodDeclaration)
        {
            var methodStart = source.IndexOf(methodDeclaration, StringComparison.Ordinal);
            Assert.True(methodStart >= 0, methodDeclaration);
            var bodyStart = source.IndexOf('{', methodStart);
            var depth = 0;
            for (var i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}' && --depth == 0) return source.Substring(bodyStart, i - bodyStart + 1);
            }

            throw new InvalidOperationException($"Unclosed method body: {methodDeclaration}");
        }

        private static string ReadSource(string relativePath)
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }
    }
}
