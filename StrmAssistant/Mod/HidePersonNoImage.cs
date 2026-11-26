using HarmonyLib;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using StrmAssistant.Common;
using StrmAssistant.Core;
using static StrmAssistant.Mod.PatchManager;
using static StrmAssistant.Options.ItemDisplayOptions;

namespace StrmAssistant.Mod
{
    public class HidePersonNoImage : PatchBase<HidePersonNoImage>
    {
        private static MethodInfo _getBaseItemDto;
        private static MethodInfo _getBaseItemDtoWithCancellation;
        private static MethodInfo _getBaseItemDtos;
        private static MethodInfo _getBaseItemDtosWithCancellation;
        private static MethodInfo _setPeople;
        private static bool _logged411Partial;

        public HidePersonNoImage()
        {
            var enabled = Plugin.Instance.ExperienceEnhanceStore.GetOptions().ItemDisplay.HidePersonNoImage;
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

            _getBaseItemDtoWithCancellation = EmbyTargetResolver.ResolveByName(dtoServiceImpl, "GetBaseItemDto",
                new[] { typeof(BaseItem), typeof(DtoOptions), typeof(User), typeof(CancellationToken) }).Method;
            _getBaseItemDtosWithCancellation = EmbyTargetResolver.ResolveByName(dtoServiceImpl, "GetBaseItemDtos",
                new[] { typeof(BaseItem[]), typeof(DtoOptions), typeof(User), typeof(CancellationToken) }).Method;

            var peopleProperty = typeof(BaseItemDto).GetProperty("People",
                BindingFlags.Public | BindingFlags.Instance);
            if (peopleProperty?.GetSetMethod(true) is MethodInfo setter)
            {
                _setPeople = setter;
            }
        }

        protected override void Prepare(bool apply)
        {
            // 4.11 的批量 GetBaseItemDtos 一旦 patch 会让 Items/首页 DTO 卡死（隔离实例实测）；
            // 只挂单条目重载，详情页过滤可用，批量列表暂不处理。
            var singleOnly = !DtoPatchPolicy.UseBatchDto(EmbyVersionAdapter.Instance.ApiVersion);

            PatchUnpatch(PatchTracker, apply, _getBaseItemDto, postfix: nameof(GetBaseItemDtoPostfix));

            if (_getBaseItemDtoWithCancellation != null)
            {
                PatchUnpatch(PatchTracker, apply, _getBaseItemDtoWithCancellation,
                    postfix: nameof(GetBaseItemDtoPostfix));
            }

            if (singleOnly)
            {
                // 4.11 的 GetBaseItemDtos（批量）patch 后会让 DTO/Items 卡死；
                // 改挂模型层 BaseItemDto.People setter，覆盖单条与批量，避免碰 DTO 生成方法。
                if (_setPeople != null)
                {
                    PatchUnpatch(PatchTracker, apply, _setPeople, prefix: nameof(SetPeoplePrefix));
                }

                if (apply && !_logged411Partial)
                {
                    _logged411Partial = true;
                    Plugin.Instance.Logger.Info(
                        "HidePersonNoImage - 4.11: batch DTO patch disabled; BaseItemDto.People setter filter active");
                }

                return;
            }

            PatchUnpatch(PatchTracker, apply, _getBaseItemDtos, postfix: nameof(GetBaseItemDtosPostfix));

            if (_getBaseItemDtosWithCancellation != null)
            {
                PatchUnpatch(PatchTracker, apply, _getBaseItemDtosWithCancellation,
                    postfix: nameof(GetBaseItemDtosPostfix));
            }
        }

        [HarmonyPostfix]
        private static void GetBaseItemDtoPostfix(BaseItem __0, BaseItemDto __result)
        {
            FilterPeople(__result, __0);
        }

        [HarmonyPostfix]
        private static void GetBaseItemDtosPostfix(BaseItem[] __0, BaseItemDto[] __result)
        {
            if (__0 == null || __result == null) return;

            for (var i = 0; i < __result.Length && i < __0.Length; i++)
            {
                FilterPeople(__result[i], __0[i]);
            }
        }

        [HarmonyPrefix]
        private static void SetPeoplePrefix(BaseItemDto __instance, ref BaseItemPerson[] value)
        {
            if (value == null || value.Length == 0) return;

            var type = __instance?.Type;
            if (string.Equals(type, nameof(Person), StringComparison.OrdinalIgnoreCase)) return;

            var preference = Plugin.Instance.ExperienceEnhanceStore.GetOptions().ItemDisplay.HidePersonPreference;

            var noImage =
                preference?.Contains(HidePersonOption.NoImage.ToString(), StringComparison.OrdinalIgnoreCase) == true;
            var actorOnly =
                preference?.Contains(HidePersonOption.ActorOnly.ToString(), StringComparison.OrdinalIgnoreCase) == true;
            var nonChinese =
                preference?.Contains(HidePersonOption.NonChinese.ToString(), StringComparison.OrdinalIgnoreCase) == true;

            if (!noImage && !actorOnly && !nonChinese) return;

            value = value.Where(p =>
                    (!noImage || p.HasPrimaryImage) &&
                    (!actorOnly || p.Type == PersonType.Actor || p.Type == PersonType.GuestStar) &&
                    (!nonChinese || LanguageUtility.IsChinese(p.Name ?? string.Empty)))
                .ToArray();
        }

        private static void FilterPeople(BaseItemDto dto, BaseItem item)
        {
            if (dto?.People == null || item == null) return;

            if (!(item is Movie) && !(item is Series) && !(item is Season) && !(item is Episode)) return;

            var preference = Plugin.Instance.ExperienceEnhanceStore.GetOptions().ItemDisplay.HidePersonPreference;

            var noImage =
                preference?.Contains(HidePersonOption.NoImage.ToString(), StringComparison.OrdinalIgnoreCase) == true;
            var actorOnly =
                preference?.Contains(HidePersonOption.ActorOnly.ToString(), StringComparison.OrdinalIgnoreCase) == true;
            var nonChinese =
                preference?.Contains(HidePersonOption.NonChinese.ToString(), StringComparison.OrdinalIgnoreCase) == true;

            if (!noImage && !actorOnly && !nonChinese) return;

            dto.People = dto.People.Where(p =>
                    (!noImage || p.HasPrimaryImage) &&
                    (!actorOnly || p.Type == PersonType.Actor || p.Type == PersonType.GuestStar) &&
                    (!nonChinese || LanguageUtility.IsChinese(p.Name ?? string.Empty)))
                .ToArray();
        }
    }
}
