using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.Core;
using System.Linq;
using System.Reflection;
using System.Threading;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class UnlockIntroSkip : PatchBase<UnlockIntroSkip>
    {
        private static MethodInfo _isIntroDetectionSupported;
        private static MethodInfo _isMarkerDetectionSupported;
        private static MethodInfo _createQueryForEpisodeIntroDetection;
        private static MethodInfo _onFailedToFindIntro;
        private static MethodInfo _detectSequences;

        private static readonly AsyncLocal<bool> LogZeroConfidence = new AsyncLocal<bool>();

        public UnlockIntroSkip()
        {
            var enabled = Plugin.Instance.IntroSkipStore.GetOptions().Fingerprint.UnlockIntroSkip;
            PatchTracker.IsFeatureEnabled = enabled;

            Initialize();

            if (enabled)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var embyProviders = Assembly.Load("Emby.Providers");
            var markerScheduledTask = embyProviders.GetType("Emby.Providers.Markers.MarkerScheduledTask");
            _createQueryForEpisodeIntroDetection = Require(EmbyTargetResolver.ResolveByName(markerScheduledTask,
                "CreateQueryForEpisodeIntroDetection", new[] { typeof(LibraryOptions) },
                typeof(InternalItemsQuery), true)).Method;

            if (EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11)
            {
                // 4.11 已把片头/片尾管线换成 MarkerDetector；旧的 AudioFingerprintManager/SequenceDetection 不存在。
                var markerDetector = embyProviders.GetType("Emby.Providers.Markers.MarkerDetector");
                _isMarkerDetectionSupported = Require(EmbyTargetResolver.ResolveByName(markerDetector,
                    "IsMarkerDetectionSupported", new[] { typeof(Video) }, typeof(bool), true)).Method;
                return;
            }

            var audioFingerprintManager = embyProviders.GetType("Emby.Providers.Markers.AudioFingerprintManager");
            _isIntroDetectionSupported = Require(EmbyTargetResolver.ResolveByName(audioFingerprintManager,
                "IsIntroDetectionSupported", new[] { typeof(Episode), typeof(LibraryOptions) },
                typeof(bool), false)).Method;

            var sequenceDetection = embyProviders.GetType("Emby.Providers.Markers.SequenceDetection");
            _detectSequences = sequenceDetection?.GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(m => m.Name == "DetectSequences" && m.GetParameters().Length == 8);
            _onFailedToFindIntro = audioFingerprintManager?.GetMethod("OnFailedToFindIntro",
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        protected override void Prepare(bool apply)
        {
            EnableImageCapture.PatchUnpatchIsShortcut(apply);

            if (_isMarkerDetectionSupported != null)
            {
                PatchUnpatch(PatchTracker, apply, _isMarkerDetectionSupported,
                    prefix: nameof(MarkerDetectionSupportedPrefix),
                    postfix: nameof(MarkerDetectionSupportedPostfix));
            }
            else
            {
                PatchUnpatch(PatchTracker, apply, _isIntroDetectionSupported,
                    prefix: nameof(IsIntroDetectionSupportedPrefix),
                    postfix: nameof(IsIntroDetectionSupportedPostfix));
            }

            PatchUnpatch(PatchTracker, apply, _createQueryForEpisodeIntroDetection,
                postfix: nameof(CreateQueryForEpisodeIntroDetectionPostfix));

            if (_detectSequences != null)
            {
                PatchUnpatch(PatchTracker, apply, _detectSequences, postfix: nameof(DetectSequencesPostfix));
            }

            if (_onFailedToFindIntro != null)
            {
                PatchUnpatch(PatchTracker, apply, _onFailedToFindIntro,
                    prefix: nameof(OnFailedToFindIntroPrefix));
            }
        }

        [HarmonyPrefix]
        private static bool MarkerDetectionSupportedPrefix(Video item, ref bool __result, out bool __state)
        {
            __state = item != null && item.IsShortcut;

            if (__state)
            {
                __result = true;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void MarkerDetectionSupportedPostfix(Video item, ref bool __result, bool __state)
        {
            if (__state)
            {
                __result = true;
            }
        }

        [HarmonyPrefix]
        private static bool IsIntroDetectionSupportedPrefix(Episode item, LibraryOptions libraryOptions,
            ref bool __result, out bool __state)
        {
            __state = false;

            if (item.IsShortcut)
            {
                EnableImageCapture.PatchIsShortcutInstance(item);
                __state = true;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void IsIntroDetectionSupportedPostfix(Episode item, LibraryOptions libraryOptions,
            ref bool __result, bool __state)
        {
            if (__state)
            {
                EnableImageCapture.UnpatchIsShortcutInstance(item);
            }
        }

        [HarmonyPostfix]
        private static void CreateQueryForEpisodeIntroDetectionPostfix(LibraryOptions libraryOptions, ref InternalItemsQuery __result)
        {
            Plugin.FingerprintApi.UpdateLibraryPathsInScope();
            var markerEnabledLibraryScope = Plugin.Instance.IntroSkipStore.GetOptions().Fingerprint.MarkerEnabledLibraryScope;

            if (!string.IsNullOrEmpty(markerEnabledLibraryScope) && markerEnabledLibraryScope.Contains("-1"))
            {
                __result.ParentIds = Plugin.FingerprintApi.GetAllFavoriteSeasons().DefaultIfEmpty(-1).ToArray();
            }
            else
            {
                if (FingerprintApi.LibraryPathsInScope.Any())
                {
                    __result.PathStartsWithAny = FingerprintApi.LibraryPathsInScope.ToArray();
                }
                
                var blackListSeasons = Plugin.FingerprintApi.GetAllBlacklistSeasons().ToArray();
                if (blackListSeasons.Any())
                {
                    __result.ExcludeParentIds = blackListSeasons;
                }
            }
        }

        [HarmonyPostfix]
        private static void DetectSequencesPostfix(object __result)
        {
            if (__result != null && Traverse.Create(__result).Property("Confidence").GetValue() is double confidence &&
                confidence == 0)
            {
                LogZeroConfidence.Value = true;
            }
        }

        [HarmonyPrefix]
        private static bool OnFailedToFindIntroPrefix(Episode episode)
        {
            if (LogZeroConfidence.Value) return true;

            BaseItem.ItemRepository.DeleteChapters(episode.InternalId,
                new[] { MarkerType.IntroStart, MarkerType.IntroEnd });

            return false;
        }
    }
}
