using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.Core;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using static StrmAssistant.Mod.PatchManager;
using static StrmAssistant.Options.MediaInfoExtractOptions;

namespace StrmAssistant.Mod
{
    public class ChapterChangeTracker : PatchBase<ChapterChangeTracker>
    {
        private static MethodInfo _saveChapters;
        private static MethodInfo _deleteChapters;
        private static MethodInfo _onFailedToFindIntro;

        private static readonly AsyncLocal<long> BypassItem = new AsyncLocal<long>();

        public ChapterChangeTracker()
        {
            var enabled = Plugin.Instance.MediaInfoExtractStore.GetOptions().MediaInfoPersist.PersistMediaInfoMode !=
                          PersistMediaInfoOption.None.ToString();
            PatchTracker.IsFeatureEnabled = enabled;

            Initialize();

            if (enabled)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var repositoryType = EmbyTargetResolver.GetImplementationType(BaseItem.ItemRepository);
            if (repositoryType == null)
            {
                repositoryType = EmbyTargetResolver.FindImplementationType(
                    Assembly.Load("Emby.Server.Implementations"), typeof(IItemRepository));
            }

            var saveWithMarkers = EmbyTargetResolver.ResolveByName(repositoryType, "SaveChapters",
                new[] { typeof(long), typeof(bool), typeof(List<ChapterInfo>), typeof(MarkerType[]) });
            var saveLegacy = EmbyTargetResolver.ResolveByName(repositoryType, "SaveChapters",
                new[] { typeof(long), typeof(bool), typeof(List<ChapterInfo>) });
            // 4.11 的 4 参重载在刷新链路中可能被 3 参重载以特殊参数调用，
            // 只挂 3 参重载可避开空键异常，同时仍覆盖正常章节写入。
            var preferLegacy = EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11;
            _saveChapters = Require(preferLegacy && saveLegacy.IsFound ? saveLegacy : saveWithMarkers.IsFound ? saveWithMarkers : saveLegacy).Method;

            _deleteChapters = EmbyTargetResolver.ResolveByName(repositoryType, "DeleteChapters",
                new[] { typeof(long), typeof(MarkerType[]) }).Method;

            // 4.11 已移除 AudioFingerprintManager；该子功能在 4.11 上静默跳过，不影响章节持久化主链路。
            var embyProviders = Assembly.Load("Emby.Providers");
            var audioFingerprintManager = embyProviders.GetType("Emby.Providers.Markers.AudioFingerprintManager");
            _onFailedToFindIntro = audioFingerprintManager?.GetMethod("OnFailedToFindIntro",
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        protected override void Prepare(bool apply)
        {
            if (EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11)
            {
                PatchTracker.MarkAlternativeActive("native chapter transaction; plugin extraction uses marker snapshot/restore");
                Plugin.Instance.Logger.Info(
                    "ChapterChangeTracker - 4.11: SaveChapters hook skipped (native chapter transaction)");
                return;
            }

            if (Plugin.Instance.MediaInfoExtractStore.GetOptions().IsModSupported)
            {
                PatchUnpatch(PatchTracker, apply, _saveChapters, postfix: nameof(SaveChaptersPostfix));
                //PatchUnpatch(PatchTracker, apply, _deleteChapters, postfix: nameof(DeleteChaptersPostfix));
                if (_onFailedToFindIntro != null)
                {
                    PatchUnpatch(PatchTracker, apply, _onFailedToFindIntro,
                        postfix: nameof(OnFailedToFindIntroPostfix));
                }
            }
        }

        public static void BypassInstance(BaseItem item)
        {
            BypassItem.Value = item.InternalId;
        }

        internal static bool IsBypassed(long itemId)
        {
            return BypassItem.Value != 0 && BypassItem.Value == itemId;
        }

        internal static void ClearBypass()
        {
            BypassItem.Value = 0;
        }

        [HarmonyPostfix]
        private static void SaveChaptersPostfix(long itemId, bool clearExtractionFailureResult,
            List<ChapterInfo> chapters)
        {
            if (chapters == null || chapters.Count == 0) return;

            if (BypassItem.Value != 0 && BypassItem.Value == itemId) return;

            _ = BackgroundTaskRunner.Run("MediaInfo.SerializeChapters",
                () => Plugin.MediaInfoApi.SerializeMediaInfo(itemId, null, true, "Save Chapters"));
        }

        [HarmonyPostfix]
        private static void DeleteChaptersPostfix(long itemId, MarkerType[] markerTypes)
        {
            if (BypassItem.Value != 0 && BypassItem.Value == itemId) return;

            _ = BackgroundTaskRunner.Run("MediaInfo.SerializeChapters",
                () => Plugin.MediaInfoApi.SerializeMediaInfo(itemId, null, true, "Delete Chapters"));
        }

        [HarmonyPostfix]
        private static void OnFailedToFindIntroPostfix(Episode episode, bool __runOriginal)
        {
            if (__runOriginal)
            {
                _ = BackgroundTaskRunner.Run("MediaInfo.SerializeChapters",
                    () => Plugin.MediaInfoApi.SerializeMediaInfo(episode.InternalId, null, true,
                        "Zero Fingerprint Confidence"));
            }
        }
    }
}
