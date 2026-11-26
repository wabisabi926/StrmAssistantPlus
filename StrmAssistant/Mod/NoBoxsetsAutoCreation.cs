using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using StrmAssistant.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class NoBoxsetsAutoCreation : PatchBase<NoBoxsetsAutoCreation>
    {
        private static MethodInfo _ensureLibraryFolder;
        private static readonly List<MethodInfo> _getUserViewsTargets = new List<MethodInfo>();

        public NoBoxsetsAutoCreation()
        {
            var enabled = Plugin.Instance.ExperienceEnhanceStore.GetOptions().LibraryOrder.NoBoxsetsAutoCreation;
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

            // 4.9/4.10：禁止 Emby 自动创建合集库；4.11 已无此方法（实测）。
            var collectionManager = embyServerImplementationsAssembly.GetType("Emby.Server.Implementations.Collections.CollectionManager");
            _ensureLibraryFolder = collectionManager?.GetMethod("EnsureLibraryFolder", BindingFlags.Instance | BindingFlags.NonPublic);

            // 用户视图过滤：4.11 的 UserViewManager 类型名被混淆，从接口实现取。
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
                    "IUserViewManager.GetUserViews -> Folder[]", "implementation method not found"));
            }

            if (_ensureLibraryFolder == null)
            {
                Plugin.Instance.Logger.Info(
                    "NoBoxsetsAutoCreation - EnsureLibraryFolder absent; user-view filter active");
            }
        }

        protected override void Prepare(bool apply)
        {
            if (_ensureLibraryFolder != null)
            {
                PatchUnpatch(PatchTracker, apply, _ensureLibraryFolder, prefix: nameof(EnsureLibraryFolderPrefix));
            }

            foreach (var target in _getUserViewsTargets)
            {
                PatchUnpatch(PatchTracker, apply, target, postfix: nameof(GetUserViewsPostfix));
            }
        }

        [HarmonyPrefix]
        private static bool EnsureLibraryFolderPrefix()
        {
            // 禁止 Emby 自动创建合集库文件夹
            return false;
        }

        /// <summary>
        /// 核心修复：移除 User 参数声明。
        /// 在 Harmony 中，如果不确定参数是否存在，不要将其写在方法签名里。
        /// 如果需要 User，可以通过 __args 或手动从 query 获取。
        /// </summary>
        /// <summary>
        /// 使用 Postfix 来过滤返回的 folders 列表，移除合集库。
        /// </summary>
        [HarmonyPostfix]
        private static void GetUserViewsPostfix(ref Folder[] __result)
        {
            if (__result == null) return;

            // 过滤掉 CollectionType 为 BoxSets 的文件夹
            __result = __result.Where(i => 
                !(i is CollectionFolder library) || 
                library.CollectionType != CollectionType.BoxSets.ToString()
            ).ToArray();
        }
    }
}