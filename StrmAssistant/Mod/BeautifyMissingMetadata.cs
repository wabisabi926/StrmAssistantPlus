using Emby.Naming.Common;
using HarmonyLib;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.Core;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class BeautifyMissingMetadata : PatchBase<BeautifyMissingMetadata>
    {
        private static MethodInfo _getBaseItemDtos;
        private static MethodInfo _getBaseItemDtosWithCancellation;
        private static MethodInfo _getBaseItemDto;
        private static MethodInfo _getBaseItemDtoWithCancellation;
        private static MethodInfo _setName;
        private static MethodInfo _setPath;
        private static readonly List<MethodInfo> _beautifySetters = new List<MethodInfo>();
        private static bool _logged411Partial;

        private static MethodInfo _getMainExpression;
        private static readonly string SeasonNumberAndEpisodeNumberExpression =
            "(?<![a-z]|[0-9])(?<seasonnumber>[0-9]+)(?:[ ._x-]*e|x|[ ._-]*ep[._ -]*|[ ._-]*episode[._ -]+)";

        public BeautifyMissingMetadata()
        {
            var enabled = Plugin.Instance.ExperienceEnhanceStore.GetOptions().ItemDisplay.BeautifyMissingMetadata;
            PatchTracker.IsFeatureEnabled = enabled;

            Initialize();

            if (enabled)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var embyServerImplementationsAssembly = Assembly.Load("Emby.Server.Implementations");
            var dtoServiceImpl = EmbyTargetResolver.FindImplementationType(
                embyServerImplementationsAssembly, typeof(IDtoService));

            _getBaseItemDto = Require(EmbyTargetResolver.ResolveByName(dtoServiceImpl, "GetBaseItemDto",
                new[] { typeof(BaseItem), typeof(DtoOptions), typeof(User) })).Method;
            if (DtoPatchPolicy.UseBatchDto(EmbyVersionAdapter.Instance.ApiVersion))
            {
                _getBaseItemDtos = Require(EmbyTargetResolver.ResolveByName(dtoServiceImpl, "GetBaseItemDtos",
                    new[] { typeof(BaseItem[]), typeof(DtoOptions), typeof(User) })).Method;
            }

            _getBaseItemDtosWithCancellation = EmbyTargetResolver.ResolveByName(dtoServiceImpl, "GetBaseItemDtos",
                new[] { typeof(BaseItem[]), typeof(DtoOptions), typeof(User), typeof(System.Threading.CancellationToken) }).Method;
            _getBaseItemDtoWithCancellation = EmbyTargetResolver.ResolveByName(dtoServiceImpl, "GetBaseItemDto",
                new[] { typeof(BaseItem), typeof(DtoOptions), typeof(User), typeof(System.Threading.CancellationToken) }).Method;

            _getMainExpression = EmbyTargetResolver.ResolveByName(typeof(NamingOptions), "GetMainExpression",
                new[] { typeof(bool), typeof(bool), typeof(bool) }, typeof(string), true).Method;

            _setName = typeof(BaseItemDto).GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                ?.GetSetMethod(true);
            _setPath = typeof(BaseItemDto).GetProperty("Path", BindingFlags.Public | BindingFlags.Instance)
                ?.GetSetMethod(true);

            _beautifySetters.Clear();
            foreach (var propertyName in new[] { "Type", "IndexNumber", "PreferredMetadataLanguage" })
            {
                var setter = typeof(BaseItemDto).GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
                if (setter != null) _beautifySetters.Add(setter);
            }
        }

        protected override void Prepare(bool apply)
        {
            var singleOnly = !DtoPatchPolicy.UseBatchDto(EmbyVersionAdapter.Instance.ApiVersion);

            PatchUnpatch(PatchTracker, apply, _getBaseItemDto, postfix: nameof(GetBaseItemDtoPostfix));

            if (_getBaseItemDtoWithCancellation != null)
            {
                PatchUnpatch(PatchTracker, apply, _getBaseItemDtoWithCancellation,
                    postfix: nameof(GetBaseItemDtoPostfix));
            }

            if (!singleOnly)
            {
                PatchUnpatch(PatchTracker, apply, _getBaseItemDtos, postfix: nameof(GetBaseItemDtosPostfix));

                if (_getBaseItemDtosWithCancellation != null)
                {
                    PatchUnpatch(PatchTracker, apply, _getBaseItemDtosWithCancellation,
                        postfix: nameof(GetBaseItemDtosPostfix));
                }
            }
            else
            {
                // 4.11 的批量 GetBaseItemDtos patch 会卡死；改挂模型 setter，Name/Path 谁后赋值都能触发。
                if (_setName != null)
                {
                    PatchUnpatch(PatchTracker, apply, _setName, postfix: nameof(SetNamePostfix));
                }

                if (_setPath != null)
                {
                    PatchUnpatch(PatchTracker, apply, _setPath, postfix: nameof(SetPathPostfix));
                }

                foreach (var setter in _beautifySetters)
                {
                    PatchUnpatch(PatchTracker, apply, setter, postfix: nameof(SetNamePostfix));
                }

                if (apply && !_logged411Partial)
                {
                    _logged411Partial = true;
                    Plugin.Instance.Logger.Info(
                        "BeautifyMissingMetadata - 4.11: batch DTO patch disabled; BaseItemDto Name/Path setter beautify active");
                }
            }

            if (_getMainExpression != null)
            {
                PatchUnpatch(PatchTracker, apply, _getMainExpression, postfix: nameof(GetMainExpressionPostfix));
            }
        }

        [HarmonyPostfix]
        private static void GetBaseItemDtosPostfix(BaseItem[] items, ref BaseItemDto[] __result)
        {
            if (items == null || items.Length == 0 || __result == null) return;

            var checkItem = items.FirstOrDefault();

            if (checkItem is null || !string.Equals(checkItem.GetPreferredMetadataLanguage(), "zh-CN",
                    StringComparison.OrdinalIgnoreCase))
                return;

            if (checkItem.ExtraType == ExtraType.AdditionalPart)
            {
                var videoCount = __result.Count(i => i.Type == nameof(Video));

                foreach (var (currentItem, index) in __result.Where(i => i.Type == nameof(Video))
                             .Select((currentItem, index) => (currentItem, index)))
                {
                    if (videoCount == 1)
                    {
                        currentItem.Name = "下部分";
                        return;
                    }

                    currentItem.Name = $"第{index + 2}部分";
                }

                return;
            }

            if (checkItem is Episode)
            {
                var episodes = !string.IsNullOrEmpty(checkItem.FileNameWithoutExtension)
                    ? items
                    : Plugin.LibraryApi.GetItemsByIds(items.Select(i => i.InternalId).ToArray());

                foreach (var (currentItem, index) in episodes.Select((currentItem, index) => (currentItem, index)))
                {
                    if (currentItem.IndexNumber.HasValue && string.Equals(currentItem.Name,
                            currentItem.FileNameWithoutExtension, StringComparison.Ordinal))
                    {
                        var matchItem = __result[index];
                        matchItem.Name = $"第 {currentItem.IndexNumber} 集";
                    }
                }
            }
        }

        [HarmonyPostfix]
        private static void GetBaseItemDtoPostfix(BaseItem item, DtoOptions options, User user,
            ref BaseItemDto __result)
        {
            if (item is Episode episode && __result != null &&
                ShouldBeautifyEpisode(episode.IndexNumber, episode.Name, episode.FileNameWithoutExtension,
                    episode.GetPreferredMetadataLanguage(), __result))
            {
                __result.Name = $"第 {episode.IndexNumber} 集";
            }
        }

        internal static bool ShouldBeautifyEpisode(int? indexNumber, string itemName, string fileNameWithoutExtension,
            string preferredMetadataLanguage, BaseItemDto dto)
        {
            return indexNumber.HasValue && dto != null &&
                   string.Equals(preferredMetadataLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(itemName, fileNameWithoutExtension, StringComparison.Ordinal);
        }

        [HarmonyPostfix]
        private static void SetNamePostfix(BaseItemDto __instance)
        {
            ApplyDtoBeautify(__instance);
        }

        [HarmonyPostfix]
        private static void SetPathPostfix(BaseItemDto __instance)
        {
            ApplyDtoBeautify(__instance);
        }

        private static void ApplyDtoBeautify(BaseItemDto dto)
        {
            if (dto == null) return;
            if (!string.Equals(dto.Type, nameof(Episode), StringComparison.OrdinalIgnoreCase)) return;
            if (!dto.IndexNumber.HasValue) return;
            if (!string.IsNullOrEmpty(dto.PreferredMetadataLanguage) &&
                !dto.PreferredMetadataLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (string.IsNullOrEmpty(dto.Path)) return;

            var fileName = Path.GetFileNameWithoutExtension(dto.Path);
            if (string.IsNullOrEmpty(fileName)) return;
            if (!string.Equals(dto.Name, fileName, StringComparison.Ordinal)) return;

            dto.Name = $"第 {dto.IndexNumber} 集";
        }

        [HarmonyPostfix]
        private static void GetMainExpressionPostfix(ref string __result, bool allowEpisodeNumberOnly,
            bool allowMultiEpisodeNumberOnlyExpression, bool allowX)
        {
            if (allowEpisodeNumberOnly && !allowMultiEpisodeNumberOnlyExpression && allowX)
            {
                __result = Regex.Replace(__result, Regex.Escape(SeasonNumberAndEpisodeNumberExpression) + @"\|?", "");
            }
        }
    }
}
