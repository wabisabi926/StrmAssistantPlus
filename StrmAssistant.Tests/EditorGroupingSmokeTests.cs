using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Editors;
using StrmAssistant.Options;
using StrmAssistant.Properties;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace StrmAssistant.Tests
{
    public class EditorGroupingSmokeTests
    {
        private readonly ITestOutputHelper _output;

        public EditorGroupingSmokeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicSettings_Regrouping_IsApplied()
        {
            var container = (EditObjectContainer)new PluginOptions().CreateEditContainer();
            Dump(container.EditorRoot, 0);

            var items = container.EditorRoot.EditorItems.ToList();
            var enableIndex = items.FindIndex(i => NameOrId(i, "EnablePlugin"));
            Assert.True(enableIndex >= 0);
            Assert.True(NameOrId(items[enableIndex + 1], "CatchupMode"));

            var chineseIndex = items.FindIndex(i => NameOrId(i, "ChineseSearch"));
            var concurrencyIndex = items.FindIndex(i => NameOrId(i, "TaskConcurrency"));
            var memoryIndex = items.FindIndex(i => NameOrId(i, "Memory"));
            var tasksIndex = items.FindIndex(i => NameOrId(i, "ScheduledTasks"));
            var diagnosticsIndex = items.FindIndex(i => NameOrId(i, "DiagnosticsMaintenance"));
            var tasks = items.OfType<EditorGroup>().FirstOrDefault(g => NameOrId(g, "ScheduledTasks"));
            Assert.NotNull(tasks);
            Assert.Equal(Resources.ScheduledTasks_EditorTitle_Common_Tasks, tasks.DisplayName);
            Assert.True(concurrencyIndex > enableIndex);
            Assert.True(chineseIndex > concurrencyIndex);
            Assert.True(memoryIndex > chineseIndex);
            Assert.True(tasksIndex > memoryIndex);
            Assert.True(diagnosticsIndex > tasksIndex);

            var concurrency = items.OfType<EditorGroup>()
                .FirstOrDefault(g => NameOrId(g, "TaskConcurrency"));
            Assert.NotNull(concurrency);
            Assert.Contains(concurrency.EditorItems, i => NameOrId(i, "MaxConcurrentCount"));
            Assert.DoesNotContain(concurrency.EditorItems, i => NameOrId(i, "CatchupMode"));

            var memory = items.OfType<EditorGroup>()
                .First(g => NameOrId(g, "Memory"));
            Assert.Equal(
                new[] { "Memory.EnableMemoryCleanup", "Memory.MemoryCleanupIntervalMinutes",
                        "Memory.EnableMemoryAutoRestart", "Memory.MemoryAutoRestartThresholdMB" },
                memory.EditorItems.Select(item => item.Id).ToArray());
            Assert.DoesNotContain(items.OfType<EditorGroup>(), g => NameOrId(g, "AdvancedMemory"));

            var diagnostics = items.OfType<EditorGroup>()
                .FirstOrDefault(g => NameOrId(g, "DiagnosticsMaintenance"));
            Assert.NotNull(diagnostics);
            Assert.Contains(diagnostics.EditorItems, i => NameOrId(i, "ConfigTransferFolder"));
            Assert.Contains(diagnostics.EditorItems, i => NameOrId(i, "CheckUpdateButton"));
            Assert.Contains(diagnostics.EditorItems.OfType<EditorButtonGroup>()
                .SelectMany(group => group.EditorItems),
                button => NameOrId(button, "RestartServerButton"));
            var configButtons = diagnostics.EditorItems.OfType<EditorButtonGroup>()
                .First(group => group.EditorItems.Any(button => NameOrId(button, "ExportConfigButton")));
            Assert.Contains(configButtons.EditorItems, button => NameOrId(button, "ExportConfigButton"));
            Assert.Contains(configButtons.EditorItems, button => NameOrId(button, "ImportConfigButton"));
        }

        [Fact]
        public void MultipleVersions_Regrouping_IsApplied()
        {
            var container = (EditObjectContainer)new ExperienceEnhanceOptions().CreateEditContainer();
            Dump(container.EditorRoot, 0);

            var multipleVersions = container.EditorRoot.EditorItems.OfType<EditorGroup>()
                .FirstOrDefault(g => NameOrId(g, "MultipleVersions"));
            Assert.NotNull(multipleVersions);
            Assert.Contains(multipleVersions.EditorItems, i => NameOrId(i, "MergeMultiVersion"));
            Assert.Contains(multipleVersions.EditorItems, i => NameOrId(i, "EnableMultiVersionEnhance"));
            Assert.DoesNotContain(multipleVersions.EditorItems.OfType<EditorGroup>(),
                g => NameOrId(g, "Merge") || NameOrId(g, "Display"));
        }

        [Fact]
        public void MediaInfo_Groups_AreSplitAndOrdered()
        {
            var container = (EditObjectContainer)new MediaInfoExtractOptions().CreateEditContainer();
            Dump(container.EditorRoot, 0);

            var groups = container.EditorRoot.EditorItems.OfType<EditorGroup>().ToList();

            // 顺序：提取范围 → 提取时机 → 持久化 → 图像 → ISO/HDR → 高级性能
            Assert.Equal(
                new[] { "ExtractionScope", "AutoOnDemand", "MediaInfoPersist",
                        "ImageCapture", "IsoHdr", "ExtractionPerformance" },
                groups.Select(g => g.Name).ToArray());

            // 不再有运行时合并组
            Assert.DoesNotContain(groups, g => NameOrId(g, "MediaInfoStoragePerformance"));
            Assert.DoesNotContain(groups, g => NameOrId(g, "StorageCapture"));

            // 独占提取排在高级性能组最前（独立控制项紧随其后）
            var perf = groups.First(g => NameOrId(g, "ExtractionPerformance"));
            Assert.StartsWith(Resources.Options_Group_Advanced, perf.DisplayName);
            Assert.True(NameOrId(perf.EditorItems.First(), "ExclusiveExtract"));
            Assert.Contains(perf.EditorItems, i => NameOrId(i, "MediaInfoExtractionTimeoutMs"));
            Assert.Contains(perf.EditorItems, i => NameOrId(i, "ImageExtractionTimeoutMultiplier"));

            var persist = groups.First(g => NameOrId(g, "MediaInfoPersist"));
            Assert.Contains(persist.EditorItems, i => i.Id == "MediaInfoPersist.PersistMediaInfoMode");
            Assert.Equal("MediaInfoPersist.ClearEmbeddedMetadata", persist.EditorItems.Last().Id);
            Assert.DoesNotContain(persist.EditorItems, i => NameOrId(i, "EnableImageCapture"));

            var capture = groups.First(g => NameOrId(g, "ImageCapture"));
            Assert.Contains(capture.EditorItems, i => NameOrId(i, "EnableImageCapture"));
            Assert.DoesNotContain(capture.EditorItems, i => NameOrId(i, "PersistMediaInfoMode"));
        }

        [Fact]
        public void Metadata_Groups_AreOrdered()
        {
            var container = (EditObjectContainer)new MetadataEnhanceOptions().CreateEditContainer();
            var groups = container.EditorRoot.EditorItems.OfType<EditorGroup>().ToList();

            Assert.Equal(
                new[] { "ChineseMetadata", "CommunityRating", "DoubanAssist", "PosterPerson",
                        "ScrapeEpisodeGroup", "EpisodeRefresh", "MetadataCache" },
                groups.Select(g => g.Name).ToArray());

            var community = groups.First(g => NameOrId(g, "CommunityRating"));
            Assert.Contains(community.EditorItems, i => NameOrId(i, "EnableMdbListCriticRating"));
            var communityItems = community.EditorItems.ToList();
            Assert.True(communityItems.FindIndex(i => NameOrId(i, "EnableMdbListCriticRating")) <
                        communityItems.FindIndex(i => NameOrId(i, "CommunityRatingMinVotes")));

            // 豆瓣合回单组：评分 → 刷新参数 → 元数据 → 连接
            var douban = groups.First(g => NameOrId(g, "DoubanAssist"));
            var doubanItems = douban.EditorItems.ToList();
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanFetchRating"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanRatingToKodi"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanRefreshIntervalDays"));
            Assert.Contains(doubanItems, i => NameOrId(i, "RatingUpdateLookbackDays"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanFillOverview"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanCastChinese"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanPosterFallback"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanCookie"));
            Assert.Contains(doubanItems, i => NameOrId(i, "DoubanAccessToken"));
            Assert.Contains(doubanItems, i => NameOrId(i, "ClearSeasonMapButton"));
            Assert.DoesNotContain(doubanItems, i => NameOrId(i, "EnableMdbListCriticRating"));

            Assert.True(doubanItems.FindIndex(i => NameOrId(i, "DoubanFetchRating")) <
                        doubanItems.FindIndex(i => NameOrId(i, "DoubanRatingToKodi")));
            Assert.True(doubanItems.FindIndex(i => NameOrId(i, "DoubanRatingToKodi")) <
                        doubanItems.FindIndex(i => NameOrId(i, "DoubanRefreshIntervalDays")));
            Assert.True(doubanItems.FindIndex(i => NameOrId(i, "DoubanRefreshIntervalDays")) <
                        doubanItems.FindIndex(i => NameOrId(i, "RatingUpdateLookbackDays")));
            Assert.True(doubanItems.FindIndex(i => NameOrId(i, "RatingUpdateLookbackDays")) <
                        doubanItems.FindIndex(i => NameOrId(i, "DoubanFillOverview")));
            Assert.True(doubanItems.FindIndex(i => NameOrId(i, "DoubanFillOverview")) <
                        doubanItems.FindIndex(i => NameOrId(i, "DoubanCookie")));

            // 合并只换容器，不复制、不丢失字段
            Assert.Equal(doubanItems.Count, doubanItems.Select(i => i.Id).Distinct().Count());

            var refresh = groups.First(g => NameOrId(g, "EpisodeRefresh"));
            Assert.Contains(refresh.EditorItems, i => NameOrId(i, "EpisodeRefreshScope"));
            Assert.DoesNotContain(refresh.EditorItems, i => NameOrId(i, "MetadataCacheMode"));

            var cache = groups.First(g => NameOrId(g, "MetadataCache"));
            Assert.Contains(cache.EditorItems, i => NameOrId(i, "MetadataCacheMode"));
            Assert.DoesNotContain(cache.EditorItems, i => NameOrId(i, "EpisodeRefreshScope"));

            var chinese = groups.First(g => NameOrId(g, "ChineseMetadata"));
            Assert.Contains(chinese.EditorItems, i => NameOrId(i, "PinyinSortName"));

            var poster = groups.First(g => NameOrId(g, "PosterPerson"));
            var posterItems = poster.EditorItems.ToList();
            Assert.DoesNotContain(posterItems, i => NameOrId(i, "PinyinSortName"));
            Assert.True(posterItems.FindIndex(i => NameOrId(i, "EnhanceMovieDbPerson")) <
                        posterItems.FindIndex(i => NameOrId(i, "ClearTagline")));
            Assert.True(posterItems.FindIndex(i => NameOrId(i, "ClearTagline")) <
                        posterItems.FindIndex(i => NameOrId(i, "RefreshPersonMode")));
        }

        [Fact]
        public void Intro_Groups_AreOrdered()
        {
            var container = (EditObjectContainer)new IntroSkipOptions().CreateEditContainer();
            var groups = container.EditorRoot.EditorItems.OfType<EditorGroup>().ToList();

            Assert.Equal(new[] { "Playback", "Fingerprint", "Scrape" },
                groups.Select(g => g.Name).ToArray());

            var fingerprintGroup = groups.First(g => NameOrId(g, "Fingerprint"));
            Assert.Equal(Resources.IntroSkipOptions_Group_Fingerprint, fingerprintGroup.DisplayName);
            var fingerprint = fingerprintGroup.EditorItems.ToList();
            Assert.True(fingerprint.FindIndex(i => NameOrId(i, "ParallelMarkerComputation")) <
                        fingerprint.FindIndex(i => NameOrId(i, "MarkerEnabledLibraryScope")));
            Assert.True(fingerprint.FindIndex(i => NameOrId(i, "MarkerEnabledLibraryScope")) <
                        fingerprint.FindIndex(i => NameOrId(i, "LibraryFingerprintMinutes")));

            var scrape = groups.First(g => NameOrId(g, "Scrape")).EditorItems.ToList();
            Assert.True(scrape.FindIndex(i => NameOrId(i, "IncludeMovies")) <
                        scrape.FindIndex(i => NameOrId(i, "LibraryScope")));
        }

        [Fact]
        public void Network_Groups_AreMerged()
        {
            var container = (EditObjectContainer)new NetworkProxyOptions().CreateEditContainer();
            var groups = container.EditorRoot.EditorItems.OfType<EditorGroup>().ToList();

            Assert.Equal(new[] { "NetworkDirectLink", "TmdbReplacement" },
                groups.Select(g => g.Name).ToArray());

            var network = groups.First(g => NameOrId(g, "NetworkDirectLink")).EditorItems.ToList();
            Assert.True(network.FindIndex(i => NameOrId(i, "ProxyServerStatus")) <
                        network.FindIndex(i => NameOrId(i, "ScrapeProxyWhitelist")));
        }

        [Fact]
        public void Experience_Groups_AreMerged()
        {
            var container = (EditObjectContainer)new ExperienceEnhanceOptions().CreateEditContainer();
            var groups = container.EditorRoot.EditorItems.OfType<EditorGroup>().ToList();

            Assert.Equal(
                new[] { "MultipleVersions", "Playback", "ItemDisplay", "LibraryOrder", "NotificationsLogs" },
                groups.Select(g => g.Name).ToArray());

            var playback = groups.First(g => NameOrId(g, "Playback"));
            var playbackItems = playback.EditorItems.ToList();
            Assert.Contains(playbackItems, i => NameOrId(i, "DryRun"));
            Assert.Contains(playbackItems, i => NameOrId(i, "EnableDeepDelete"));
            Assert.True(playbackItems.FindIndex(i => NameOrId(i, "DryRun")) >
                        playbackItems.FindIndex(i => NameOrId(i, "EnableDeepDelete")));
            Assert.Contains(playbackItems, i => i.Id == "Playback.LibraryScopePlayProgress");
            Assert.DoesNotContain(groups, g => NameOrId(g, "AdvancedPlayback"));
            Assert.DoesNotContain(groups, g => NameOrId(g, "Safety"));

            var versions = groups.First(g => NameOrId(g, "MultipleVersions")).EditorItems.ToList();
            Assert.True(versions.FindIndex(i => NameOrId(i, "MultiVersionSortOption")) <
                        versions.FindIndex(i => NameOrId(i, "SplitMoviesButton")));
            Assert.True(versions.FindIndex(i => NameOrId(i, "SplitMoviesButton")) <
                        versions.FindIndex(i => NameOrId(i, "SplitMoviesProgress")));

            var itemDisplay = groups.First(g => NameOrId(g, "ItemDisplay"));
            var notifications = groups.First(g => NameOrId(g, "NotificationsLogs"));
            Assert.Equal(Resources.ExperienceEnhanceOptions_Group_NotificationAndLogs, notifications.DisplayName);
            Assert.Contains(itemDisplay.EditorItems, i => NameOrId(i, "ShowMediaInfo"));
            Assert.Contains(itemDisplay.EditorItems, i => NameOrId(i, "BlockSearchRecommend"));
            Assert.DoesNotContain(notifications.EditorItems, i => NameOrId(i, "ShowMediaInfo"));
            Assert.DoesNotContain(notifications.EditorItems, i => NameOrId(i, "BlockSearchRecommend"));
            Assert.Contains(notifications.EditorItems, i => NameOrId(i, "ReverseLogLines"));
        }

        [Fact]
        public void DiagnosticsMaintenance_ContainsHealthAndConfigActions()
        {
            var container = (EditObjectContainer)new PluginOptions().CreateEditContainer();
            var diagnostics = container.EditorRoot.EditorItems.OfType<EditorGroup>()
                .FirstOrDefault(g => NameOrId(g, "DiagnosticsMaintenance"));

            Assert.NotNull(diagnostics);
            Assert.DoesNotContain(diagnostics.EditorItems.OfType<EditorStatusItem>(),
                item => NameOrId(item, "OverviewStatus"));

            var buttons = diagnostics.EditorItems.OfType<EditorButtonItem>()
                .Concat(diagnostics.EditorItems.OfType<EditorButtonGroup>()
                    .SelectMany(group => group.EditorItems))
                .ToList();
            Assert.Contains(buttons, item => NameOrId(item, "MediaHealthCheckButton"));
            Assert.Contains(buttons, item => NameOrId(item, "ExportConfigButton"));
            Assert.Contains(buttons, item => NameOrId(item, "CheckUpdateButton"));
            Assert.Equal("RuntimeStatus.RestartServerButton", buttons.Last().Id);
        }

        [Fact]
        public void Basic_ActionButtons_AreOrdered()
        {
            var container = (EditObjectContainer)new PluginOptions().CreateEditContainer();
            var groups = container.EditorRoot.EditorItems.OfType<EditorGroup>().ToList();

            var tasks = groups.First(g => NameOrId(g, "ScheduledTasks"));
            var taskButtons = tasks.EditorItems.OfType<EditorButtonGroup>().Single().EditorItems.ToList();
            Assert.Equal(
                new[] { "ExtractMediaInfoButton", "RefreshEpisodesButton", "MergeMultiVersionButton",
                        "RebuildUserDataKeysButton" },
                taskButtons.Select(b => b.Name).ToArray());

            var maintenance = groups.First(g => NameOrId(g, "DiagnosticsMaintenance"));
            var maintenanceButtons = maintenance.EditorItems.OfType<EditorButtonGroup>().First()
                .EditorItems.ToList();
            Assert.Equal(
                new[] { "VerifyMetadataCacheButton", "MediaHealthCheckButton", "ExportMediaHealthButton",
                        "CleanMemoryNowButton", "ScheduleIndexRebuildButton", "OptimizeSearchIndexButton",
                        "ClearMetadataCacheButton" },
                maintenanceButtons.Select(b => b.Name).ToArray());
            Assert.Equal("RestartServerButton", maintenance.EditorItems.OfType<EditorButtonGroup>()
                .SelectMany(group => group.EditorItems)
                .Last().Name);
        }

        [Fact]
        public void PageDescriptions_AreUniformlyEmpty()
        {
            var pages = new EditableOptionsBase[]
            {
                new PluginOptions(),
                new ExperienceEnhanceOptions(),
                new MediaInfoExtractOptions(),
                new MetadataEnhanceOptions(),
                new IntroSkipOptions(),
                new NetworkProxyOptions()
            };

            Assert.All(pages, page => Assert.True(string.IsNullOrEmpty(page.EditorDescription)));
        }

        private static bool NameOrId(EditorBase editor, string name)
        {
            if (editor == null) return false;
            if (editor.Name == name) return true;
            var id = editor.Id;
            return id == name || id?.EndsWith("." + name) == true;
        }

        private void Dump(EditorBase editor, int depth)
        {
            _output.WriteLine(new string(' ', depth * 2) +
                              $"{editor.GetType().Name} name={editor.Name} display={editor.DisplayName} id={editor.Id}");
            if (editor is EditorGroup group && group.EditorItems != null)
            {
                foreach (var child in group.EditorItems) Dump(child, depth + 1);
            }
        }
    }
}
