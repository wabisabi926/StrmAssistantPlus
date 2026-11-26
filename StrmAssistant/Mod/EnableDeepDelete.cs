using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using StrmAssistant.Common;
using StrmAssistant.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class EnableDeepDelete : PatchBase<EnableDeepDelete>
    {
        private static MethodInfo _deleteItem;

        public EnableDeepDelete()
        {
            var enabled = Plugin.Instance.ExperienceEnhanceStore.GetOptions().Playback.EnableDeepDelete;
            PatchTracker.IsFeatureEnabled = enabled;

            Initialize();

            if (enabled)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var libraryManagerType = EmbyTargetResolver.GetImplementationType(BaseItem.LibraryManager) ??
                                     EmbyTargetResolver.FindImplementationType(
                                         Assembly.Load("Emby.Server.Implementations"),
                                         typeof(MediaBrowser.Controller.Library.ILibraryManager));

            _deleteItem = Require(EmbyTargetResolver.ResolveByName(libraryManagerType, "DeleteItem",
                new[] { typeof(BaseItem), typeof(DeleteOptions), typeof(BaseItem), typeof(bool) })).Method;
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _deleteItem, prefix: nameof(DeleteItemPrefix),
                finalizer: nameof(DeleteItemFinalizer));
        }

        [HarmonyPrefix]
        private static bool DeleteItemPrefix(ILibraryManager __instance, BaseItem item, DeleteOptions options,
            BaseItem parent, bool notifyParentItem, out Dictionary<string, bool?> __state)
        {
            __state = null;

            if (DryRunMode.Skip($"delete item (deep delete): {item?.Name}", Plugin.Instance.Logger))
                return false;

            if (options.DeleteFileLocation)
            {
                var collectionFolder = options.CollectionFolders ?? __instance.GetCollectionFolders(item);
                var scope = item.GetDeletePaths(true, collectionFolder).Select(i => i.FullName).ToArray();

                __state = Plugin.LibraryApi.PrepareDeepDelete(item, scope);
            }

            return true;
        }

        [HarmonyFinalizer]
        private static void DeleteItemFinalizer(Exception __exception, Dictionary<string, bool?> __state)
        {
            if (__state != null && __state.Count > 0 && __exception is null)
            {
                var localMountPaths = new HashSet<string>(__state.Where(kv => kv.Value is true).Select(kv => kv.Key));

                if (localMountPaths.Count > 0)
                {
                    Task.Run(() => Plugin.LibraryApi.ExecuteDeepDelete(localMountPaths)).ConfigureAwait(false);
                }
            }
        }
    }
}
