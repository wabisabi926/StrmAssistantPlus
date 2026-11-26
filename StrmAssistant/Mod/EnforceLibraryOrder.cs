using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using StrmAssistant.Common;
using StrmAssistant.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class EnforceLibraryOrder : PatchBase<EnforceLibraryOrder>
    {
        private static readonly List<MethodInfo> _getUserViewsTargets = new List<MethodInfo>();

        public EnforceLibraryOrder()
        {
            var enabled = Plugin.Instance.ExperienceEnhanceStore.GetOptions().LibraryOrder.EnforceLibraryOrder;
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
            var userViewManagerImpl = EmbyTargetResolver.FindImplementationType(
                embyServerImplementationsAssembly, typeof(IUserViewManager));

            if (userViewManagerImpl != null)
            {
                foreach (var method in userViewManagerImpl.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                             .Where(m => m.Name == "GetUserViews" && m.ReturnType == typeof(Folder[])))
                {
                    _getUserViewsTargets.Add(method);
                }
            }

            if (_getUserViewsTargets.Count == 0)
            {
                Require(TargetResolution.Missing(
                    "IUserViewManager.GetUserViews(UserViewQuery[, ...]) -> Folder[]",
                    "implementation method not found"));
            }
        }

        protected override void Prepare(bool apply)
        {
            foreach (var target in _getUserViewsTargets)
            {
                PatchUnpatch(PatchTracker, apply, target, postfix: nameof(GetUserViewsPostfix));
            }
        }

        [HarmonyPostfix]
        private static void GetUserViewsPostfix(ref Folder[] __result)
        {
            try
            {
                if (__result == null || __result.Length == 0 || LibraryApi.AdminOrderedViews == null ||
                    LibraryApi.AdminOrderedViews.Length == 0)
                {
                    return;
                }

                // 根据 AdminOrderedViews 对 Folder 数组进行排序
                var orderedViews = LibraryApi.AdminOrderedViews;
                var sortedList = new List<Folder>();
                var remainingFolders = __result.ToList();

                // 先按照 AdminOrderedViews 的顺序添加
                foreach (var orderedViewId in orderedViews)
                {
                    var folder = remainingFolders.FirstOrDefault(f => f.Id.ToString("N") == orderedViewId);
                    if (folder != null)
                    {
                        sortedList.Add(folder);
                        remainingFolders.Remove(folder);
                    }
                }

                // 将剩余未排序的文件夹添加到末尾
                sortedList.AddRange(remainingFolders);

                __result = sortedList.ToArray();
            }
            catch (Exception ex)
            {
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"EnforceLibraryOrder GetUserViewsPostfix error: {ex.Message}");
                }
            }
        }
    }
}
