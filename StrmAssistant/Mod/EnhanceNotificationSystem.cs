using Emby.Notifications;
using HarmonyLib;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StrmAssistant.Common;
using StrmAssistant.Core;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class EnhanceNotificationSystem: PatchBase<EnhanceNotificationSystem>
    {
        private static MethodInfo _convertToGroups;
        private static MethodInfo _sendNotification;
        private static MethodInfo _queueNotification;
        private static MethodInfo _deleteItemsRequest;
        private static MethodInfo _getUserForRequest;
        private static MethodInfo _deleteItem;

        private static readonly AsyncLocal<Dictionary<long, List<(int? IndexNumber, int? ParentIndexNumber)>>>
            GroupDetails = new AsyncLocal<Dictionary<long, List<(int? IndexNumber, int? ParentIndexNumber)>>>();
        private static readonly AsyncLocal<string> Description = new AsyncLocal<string>();
        private static readonly AsyncLocal<User> DeleteByUser = new AsyncLocal<User>();

        public EnhanceNotificationSystem()
        {
            var enabled = Plugin.Instance.ExperienceEnhanceStore.GetOptions().Notification.EnhanceNotificationSystem;
            PatchTracker.IsFeatureEnabled = enabled;

            Initialize();

            if (enabled)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var notificationsAssembly = Assembly.Load("Emby.Notifications");
            var notificationManager = notificationsAssembly.GetType("Emby.Notifications.NotificationManager");

            if (notificationManager != null)
            {
                // 4.11 私有方法名被混淆，按签名定位；4.9/4.10 名称匹配仍可用作兜底。
                _convertToGroups = EmbyTargetResolver.ResolveBySignature(notificationManager,
                    new[] { typeof(ItemChangeEventArgs[]) },
                    typeof(Dictionary<long, List<ItemChangeEventArgs>>), false).Method ??
                    notificationManager.GetMethod("ConvertToGroups",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                _sendNotification = EmbyTargetResolver.ResolveBySignature(notificationManager,
                    new[] { typeof(INotifier), typeof(NotificationInfo[]), typeof(NotificationRequest), typeof(bool) },
                    typeof(void), false).Method ??
                    notificationManager.GetMethod("SendNotification",
                        BindingFlags.NonPublic | BindingFlags.Instance, null,
                        new[] { typeof(INotifier), typeof(NotificationInfo[]), typeof(NotificationRequest), typeof(bool) },
                        null);
            }

            var notificationQueueManager = notificationsAssembly.GetType("Emby.Notifications.NotificationQueueManager");
            _queueNotification = notificationQueueManager?.GetMethod("QueueNotification",
                BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(INotifier), typeof(InternalNotificationRequest), typeof(int) }, null);

            var embyApi = Assembly.Load("Emby.Api");
            var libraryService = embyApi.GetType("Emby.Api.Library.LibraryService");
            _deleteItemsRequest =
                libraryService.GetMethod("Any", new[] { embyApi.GetType("Emby.Api.Library.DeleteItems") });
            _getUserForRequest = typeof(BaseApiService).GetMethod("GetUserForRequest",
                BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(bool) }, null);
            ReversePatch(PatchTracker, _getUserForRequest, nameof(GetUserForRequestStub));

            var embyServerImplementationsAssembly = Assembly.Load("Emby.Server.Implementations");
            var libraryManager =
                embyServerImplementationsAssembly.GetType("Emby.Server.Implementations.Library.LibraryManager") ??
                EmbyTargetResolver.FindImplementationType(embyServerImplementationsAssembly, typeof(ILibraryManager));
            _deleteItem = libraryManager?.GetMethod("DeleteItem",
                BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(BaseItem), typeof(DeleteOptions), typeof(BaseItem), typeof(bool) }, null);
        }

        protected override void Prepare(bool apply)
        {
            if (EmbyVersionAdapter.Instance.ApiVersion < EmbyApiVersion.V4_11)
            {
                if (_convertToGroups != null)
                    PatchUnpatch(PatchTracker, apply, _convertToGroups, postfix: nameof(ConvertToGroupsPostfix));
                if (_sendNotification != null)
                    PatchUnpatch(PatchTracker, apply, _sendNotification, prefix: nameof(SendNotificationPrefix));
                if (_queueNotification != null)
                    PatchUnpatch(PatchTracker, apply, _queueNotification, prefix: nameof(QueueNotificationPrefix));
            }
            else
            {
                Plugin.Instance.Logger.Info(
                    "EnhanceNotificationSystem - series summary injection skipped; native notifications and custom notifications remain available");
            }

            if (_deleteItemsRequest != null)
                PatchUnpatch(PatchTracker, apply, _deleteItemsRequest, prefix: nameof(DeleteItemsRequestPrefix),
                    postfix: nameof(DeleteItemsRequestPostfix));
            if (_deleteItem != null)
                PatchUnpatch(PatchTracker, apply, _deleteItem, prefix: nameof(DeleteItemPrefix),
                    finalizer: nameof(DeleteItemFinalizer));
        }

        [HarmonyPostfix]
        private static void ConvertToGroupsPostfix(ItemChangeEventArgs[] __0,
            ref Dictionary<long, List<ItemChangeEventArgs>> __result)
        {
            var filteredItems = __0?.Where(i => i.Item.SeriesId != 0L).ToArray() ?? Array.Empty<ItemChangeEventArgs>();

            if (filteredItems.Length == 0) return;

            GroupDetails.Value = filteredItems.GroupBy(i => i.Item.SeriesId)
                .ToDictionary(g => g.Key, g => g.Select(i => (i.Item.IndexNumber, i.Item.ParentIndexNumber)).ToList());
        }

        [HarmonyPrefix]
        private static void SendNotificationPrefix(INotifier __0, NotificationInfo[] __1,
            NotificationRequest __2, bool __3)
        {
            var notifications = __1;
            var request = __2;
            if (notifications?.FirstOrDefault()?.GroupItems is true
                && request?.Item is Series series && GroupDetails.Value != null
                && GroupDetails.Value.TryGetValue(series.InternalId, out var groupDetails))
            {
                var groupedBySeason = groupDetails.Where(e => e.ParentIndexNumber.HasValue)
                    .GroupBy(e => e.ParentIndexNumber)
                    .OrderBy(g => g.Key)
                    .ToList();

                var descriptions = new List<string>();
                foreach (var seasonGroup in groupedBySeason)
                {
                    var episodesBySeason = seasonGroup
                        .Where(e => e.IndexNumber.HasValue)
                        .OrderBy(e => e.IndexNumber.Value)
                        .Select(e => e.IndexNumber.Value)
                        .Distinct()
                        .ToList();

                    if (episodesBySeason.Count == 0) continue;

                    var episodeRanges = new List<string>();
                    var rangeStart = episodesBySeason[0];
                    var lastEpisodeInRange = rangeStart;

                    for (var i = 1; i < episodesBySeason.Count; i++)
                    {
                        var current = episodesBySeason[i];
                        if (current != lastEpisodeInRange + 1)
                        {
                            episodeRanges.Add(rangeStart == lastEpisodeInRange
                                ? $"E{rangeStart:D2}"
                                : $"E{rangeStart:D2}-E{lastEpisodeInRange:D2}");
                            rangeStart = current;
                        }

                        lastEpisodeInRange = current;
                    }

                    episodeRanges.Add(rangeStart == lastEpisodeInRange
                        ? $"E{rangeStart:D2}"
                        : $"E{rangeStart:D2}-E{lastEpisodeInRange:D2}");
                    descriptions.Add($"S{seasonGroup.Key:D2} {string.Join(", ", episodeRanges)}");
                }

                var summary = string.Join(" / ", descriptions);
                var tmdbId = series.GetProviderId(MetadataProviders.Tmdb);
                if (!string.IsNullOrEmpty(tmdbId))
                {
                    summary += $"{Environment.NewLine}{Environment.NewLine}TmdbId: {tmdbId}";
                }

                Description.Value = summary;
            }
        }

        [HarmonyPrefix]
        private static void QueueNotificationPrefix(INotifier sender, InternalNotificationRequest request, int priority)
        {
            if (!string.IsNullOrEmpty(Description.Value))
            {
                request.Description = Description.Value;
                Description.Value = null;
            }
        }

        [HarmonyReversePatch]
        private static User GetUserForRequestStub(BaseApiService instance, string requestedUserId,
            bool autoRevertToLoggedInUser = true) => throw new NotImplementedException();

        [HarmonyPrefix]
        private static void DeleteItemsRequestPrefix(BaseApiService __instance, IReturnVoid request)
        {
            DeleteByUser.Value = GetUserForRequestStub(__instance, null);
        }

        [HarmonyPostfix]
        private static void DeleteItemsRequestPostfix(BaseApiService __instance, IReturnVoid request)
        {
            DeleteByUser.Value = null;
        }

        [HarmonyPrefix]
        private static void DeleteItemPrefix(ILibraryManager __instance, BaseItem item, DeleteOptions options,
            BaseItem parent, bool notifyParentItem, out Dictionary<string, bool?> __state)
        {
            __state = null;

            if (options.DeleteFileLocation && item != null)
            {
                var collectionFolder = options.CollectionFolders ?? __instance.GetCollectionFolders(item);
                var scope = item.GetDeletePaths(true, collectionFolder).Select(i => i.FullName).ToArray();

                __state = Plugin.LibraryApi.PrepareDeepDelete(item, scope);
            }
        }

        internal static bool ShouldNotifyDelete(Exception exception, Dictionary<string, bool?> state, bool dryRun)
        {
            return !dryRun && exception is null && state != null && state.Count > 0;
        }

        [HarmonyFinalizer]
        private static void DeleteItemFinalizer(Exception __exception, BaseItem item, Dictionary<string, bool?> __state)
        {
            if (ShouldNotifyDelete(__exception, __state, DryRunMode.Enabled) && DeleteByUser.Value != null)
            {
                var user = DeleteByUser.Value;

                Task.Run(() =>
                        Plugin.NotificationApi.DeepDeleteSendNotification(item, user,
                            new HashSet<string>(__state.Keys)))
                    .ConfigureAwait(false);
            }
        }
    }
}
