using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 元数据刷新保护：Emby 重新探测章节时会用新列表整体替换 Chapters3，
    /// 导致 IntroStart/IntroEnd/CreditsStart 被清掉。这里在 SaveChapters 前把
    /// 新列表里缺失的标记类型从旧章节合并回来；用户主动清除标记时走
    /// <see cref="ChapterChangeTracker.BypassInstance"/>，不会被复活。
    /// </summary>
    public class IntroMarkerProtector : PatchBase<IntroMarkerProtector>
    {
        private static MethodInfo _saveChapters;
        private static MethodInfo _refreshSingleItem;
        private static readonly AsyncLocal<long> BypassItem = new AsyncLocal<long>();

        private sealed class RefreshMarkerState
        {
            public BaseItem Item { get; set; }
            public List<ChapterInfo> Markers { get; set; }
        }

        private static readonly MarkerType[] ProtectedMarkerTypes =
        {
            MarkerType.IntroStart,
            MarkerType.IntroEnd,
            MarkerType.CreditsStart
        };

        public IntroMarkerProtector()
        {
            Initialize();
            Patch();
        }

        protected override void OnInitialize()
        {
            if (EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11)
            {
                var providerManager = Assembly.Load("Emby.Providers")
                    .GetType("Emby.Providers.Manager.ProviderManager");
                _refreshSingleItem = Require(EmbyTargetResolver.ResolveByName(providerManager, "RefreshSingleItem",
                    new[]
                    {
                        typeof(BaseItem), typeof(MetadataRefreshOptions), typeof(BaseItem[]),
                        typeof(LibraryOptions), typeof(CancellationToken)
                    }, typeof(Task<ItemUpdateType>))).Method;

                Plugin.Instance.Logger.Info(
                    "IntroMarkerProtector - 4.11: SaveChapters hook skipped; ProviderManager refresh snapshot/restore active");
                return;
            }

            var repositoryType = EmbyTargetResolver.GetImplementationType(BaseItem.ItemRepository);
            if (repositoryType == null)
            {
                repositoryType = EmbyTargetResolver.FindImplementationType(
                    Assembly.Load("Emby.Server.Implementations"), typeof(MediaBrowser.Controller.Persistence.IItemRepository));
            }

            _saveChapters = Require(EmbyTargetResolver.ResolveByName(repositoryType, "SaveChapters",
                new[] { typeof(long), typeof(bool), typeof(List<ChapterInfo>) })).Method;

            if (_saveChapters == null)
            {
                Plugin.Instance.Logger.Warn(
                    "IntroMarkerProtector - IItemRepository.SaveChapters not found; marker protection disabled");
            }
        }

        protected override void Prepare(bool apply)
        {
            if (EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11)
            {
                PatchUnpatch(PatchTracker, apply, _refreshSingleItem,
                    prefix: nameof(RefreshSingleItem411Prefix), postfix: nameof(RefreshSingleItem411Postfix));
                PatchTracker.MarkAlternativeActive("ProviderManager refresh snapshot/restore");
                return;
            }
            PatchUnpatch(PatchTracker, apply, _saveChapters, prefix: nameof(SaveChaptersPrefix));
        }

        /// <summary>
        /// 仅用于「用户主动清除」和「从 JSON 恢复章节」这类需要覆盖标记的写入。
        /// 媒体信息提取/预取只 bypass ChapterChangeTracker，不再连带关闭标记保护。
        /// </summary>
        public static void BypassInstance(BaseItem item)
        {
            BypassItem.Value = item?.InternalId ?? 0;
        }

        internal static bool IsBypassed(long itemId)
        {
            return BypassItem.Value != 0 && BypassItem.Value == itemId;
        }

        internal static void ClearBypass()
        {
            BypassItem.Value = 0;
        }

        [HarmonyPrefix]
        private static void SaveChaptersPrefix(long itemId, List<ChapterInfo> chapters)
        {
            if (itemId <= 0 || chapters == null) return;
            if (IsBypassed(itemId)) return;

            // 三种标记都已存在时不需要再查旧章节
            if (ProtectedMarkerTypes.All(t => chapters.Any(c => c.MarkerType == t))) return;

            try
            {
                var item = BaseItem.LibraryManager.GetItemById(itemId);
                if (item == null) return;
                if (!(item is Episode) && !(item is Movie)) return;

                var existing = BaseItem.ItemRepository.GetChapters(item);
                var merged = MergeMissingMarkers(chapters, existing);
                if (merged.Count == chapters.Count) return;

                var added = merged.Count - chapters.Count;
                chapters.Clear();
                chapters.AddRange(merged);

                Plugin.Instance.Logger.Info(
                    $"IntroMarkerProtector - preserved {added} intro/credits marker(s) for {item.Name}");
            }
            catch (Exception e)
            {
                // 保护失败不能影响正常章节写入；最坏结果与未启用保护相同
                Plugin.Instance.Logger.Debug("IntroMarkerProtector - merge failed: " + e.Message);
            }
        }


        [HarmonyPrefix]
        private static void RefreshSingleItem411Prefix(BaseItem __0, ref RefreshMarkerState __state)
        {
            if (!(__0 is Episode) && !(__0 is Movie)) return;

            __state = new RefreshMarkerState
            {
                Item = __0,
                Markers = CaptureProtectedMarkers(__0)
            };
        }

        [HarmonyPostfix]
        private static void RefreshSingleItem411Postfix(ref Task<ItemUpdateType> __result, RefreshMarkerState __state)
        {
            if (__result == null || __state?.Item == null || __state.Markers.Count == 0) return;
            __result = RestoreAfterRefreshAsync(__result, __state.Item, __state.Markers);
        }

        internal static async Task<ItemUpdateType> RestoreAfterRefreshAsync(Task<ItemUpdateType> refreshTask,
            BaseItem item, List<ChapterInfo> markers)
        {
            try
            {
                return await refreshTask.ConfigureAwait(false);
            }
            finally
            {
                RestoreProtectedMarkers(item, markers);
            }
        }

        internal static List<ChapterInfo> CaptureProtectedMarkers(BaseItem item)
        {
            try
            {
                if (item == null) return new List<ChapterInfo>();
                var chapters = BaseItem.ItemRepository.GetChapters(item) ?? new List<ChapterInfo>();
                var runtime = item.RunTimeTicks;
                return chapters.Where(c => IsRestorableMarker(c, chapters, runtime)).ToList();
            }
            catch (Exception e)
            {
                Plugin.Instance?.Logger?.Warn("IntroMarkerProtector - capture failed: " + e.Message);
                return new List<ChapterInfo>();
            }
        }

        internal static bool RestoreProtectedMarkers(BaseItem item, List<ChapterInfo> cached)
        {
            if (item == null || cached == null || cached.Count == 0) return false;
            if (IsBypassed(item.InternalId)) return false;

            try
            {
                var current = BaseItem.ItemRepository.GetChapters(item) ?? new List<ChapterInfo>();
                var merged = MergeMissingMarkers(current, cached);
                if (merged.Count == current.Count) return false;

                var added = merged.Count - current.Count;
                BaseItem.ItemRepository.SaveChapters(item.InternalId, false, merged);
                Plugin.Instance.Logger.Info(
                    $"IntroMarkerProtector - restored {added} intro/credits marker(s) after refresh for {item.Name}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Instance?.Logger?.Warn("IntroMarkerProtector - restore failed: " + e.Message);
                return false;
            }
        }

        internal static bool IsRestorableMarker(ChapterInfo chapter, IReadOnlyList<ChapterInfo> chapters,
            long? runtimeTicks)
        {
            if (chapter == null || !IsDbMarker(chapter)) return chapter != null;
            if (chapter.MarkerType == MarkerType.CreditsStart)
                return ChapterApi.IsValidCreditsStart(chapter.StartPositionTicks, runtimeTicks);

            var introStarts = chapters.Where(c => c?.MarkerType == MarkerType.IntroStart && IsDbMarker(c)).ToList();
            var introEnds = chapters.Where(c => c?.MarkerType == MarkerType.IntroEnd && IsDbMarker(c)).ToList();
            if (introStarts.Count > 0 && introEnds.Count > 0)
            {
                return introStarts.Count == 1 && introEnds.Count == 1 &&
                       ChapterApi.IsValidIntroRange(introStarts[0].StartPositionTicks,
                           introEnds[0].StartPositionTicks, runtimeTicks);
            }

            if (chapter.MarkerType == MarkerType.IntroStart)
                return chapter.StartPositionTicks >= 0 &&
                       (!runtimeTicks.HasValue || chapter.StartPositionTicks < runtimeTicks.Value);
            if (chapter.MarkerType == MarkerType.IntroEnd)
                return chapter.StartPositionTicks > 0 &&
                       (!runtimeTicks.HasValue || chapter.StartPositionTicks <= runtimeTicks.Value);
            return true;
        }

        private static bool IsDbMarker(ChapterInfo chapter) =>
            chapter?.Name != null && chapter.Name.EndsWith("#DB", StringComparison.Ordinal);

        /// <summary>
        /// 新章节列表里没有出现的标记类型，从旧列表中补回；同一类型以新列表为准。
        /// 纯函数便于单测。
        /// </summary>
        internal static List<ChapterInfo> MergeMissingMarkers(IReadOnlyList<ChapterInfo> incoming,
            IReadOnlyList<ChapterInfo> existing)
        {
            var result = incoming?.ToList() ?? new List<ChapterInfo>();
            if (existing == null || existing.Count == 0) return result;

            var incomingTypes = new HashSet<MarkerType>(
                result.Where(c => ProtectedMarkerTypes.Contains(c.MarkerType)).Select(c => c.MarkerType));

            var missing = existing
                .Where(c => ProtectedMarkerTypes.Contains(c.MarkerType) && !incomingTypes.Contains(c.MarkerType))
                .ToList();

            if (missing.Count == 0) return result;

            result.AddRange(missing);
            result.Sort((c1, c2) => c1.StartPositionTicks.CompareTo(c2.StartPositionTicks));
            return result;
        }
    }
}
