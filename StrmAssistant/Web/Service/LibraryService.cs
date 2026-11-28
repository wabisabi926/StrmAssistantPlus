using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using StrmAssistant.Common;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static StrmAssistant.Common.CommonUtility;
using static StrmAssistant.Common.LanguageUtility;

namespace StrmAssistant.Web.Service
{
    public class LibraryService : BaseApiService
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;
        private readonly IItemRepository _itemRepository;

        public LibraryService(ILibraryManager libraryManager, IItemRepository itemRepository, IFileSystem fileSystem)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _itemRepository = itemRepository;
            _fileSystem = fileSystem;
        }

        public DeleteVersionResult Any(DeleteVersion request)
        {
            if (!Plugin.IsPluginEnabled) return new DeleteVersionResult { Status = "NoAction" };

            var item = _libraryManager.GetItemById(request.Id);

            if (!(item is Video video) || !(video is Movie || video is Episode) || !video.IsFileProtocol ||
                video.GetAlternateVersionIds().Count == 0)
            {
                return new DeleteVersionResult { Status = "NoAction" };
            }
            
            var user = GetUserForRequest(null);

            bool CanDeleteItem(BaseItem target)
            {
                var folders = _libraryManager.GetCollectionFolders(target);
                return user == null ? target.CanDelete() : target.CanDelete(user, folders);
            }

            var candidates = item is Episode episode && request.DeleteParent
                ? GetSeasonEpisodesSameVersion(episode)
                : new List<BaseItem> { item };
            var deletePlan = VersionDeletePlanner.Build(item, candidates, CanDeleteItem);
            if (deletePlan.IsDenied || deletePlan.UnauthorizedCount > 0)
            {
                _logger.Info("DeleteVersion - delete plan rejected because one or more items are not authorized");
                return new DeleteVersionResult
                {
                    Status = "Denied",
                    WouldDeleteCount = deletePlan.Authorized.Count,
                    SkippedCount = deletePlan.UnauthorizedCount
                };
            }

            var deleteItems = deletePlan.Authorized.ToList();
            var enableDeepDelete = Plugin.Instance.ExperienceEnhanceStore.GetOptions().Playback.EnableDeepDelete;
            var enableNotification = Plugin.Instance.ExperienceEnhanceStore.GetOptions().Notification.EnhanceNotificationSystem;

            if (DryRunMode.Skip($"delete {deleteItems.Count} version item(s)", _logger))
            {
                return new DeleteVersionResult
                {
                    Status = "DryRun",
                    WouldDeleteCount = deleteItems.Count,
                    SkippedCount = deletePlan.UnauthorizedCount
                };
            }

            var allMountPaths = new Dictionary<string, bool?>();
            var authorizedDeleteScopes = new List<string[]>();
            var deleteFiles = new List<string>();
            var metadataDirectories = new List<string>();

            foreach (var deleteItem in deleteItems)
            {
                var deleteScope = GetDeepDeleteScope(deleteItem);
                if (deleteScope.Length > 0) authorizedDeleteScopes.Add(deleteScope);
                foreach (var path in Plugin.LibraryApi.GetDeletePaths(deleteItem)
                             .Where(p => !p.IsDirectory &&
                                         (deleteScope.Length == 0 ||
                                          PathBoundary.IsLocalFileInScope(p.FullName, deleteScope))))
                {
                    deleteFiles.Add(path.FullName);
                }

                var metadataPath = deleteItem.GetInternalMetadataPath();
                if (!string.IsNullOrEmpty(metadataPath) && _fileSystem.DirectoryExists(metadataPath))
                {
                    metadataDirectories.Add(metadataPath);
                }

                if (enableDeepDelete || enableNotification)
                {
                    var mountPaths = Plugin.LibraryApi.PrepareDeepDelete(deleteItem, GetDeepDeleteScope(deleteItem));
                    foreach (var pair in mountPaths)
                    {
                        allMountPaths.TryAdd(pair.Key, pair.Value);
                    }
                }
            }

            if (enableDeepDelete)
            {
                foreach (var mountPath in allMountPaths.Where(p => p.Value is true).Select(p => p.Key))
                {
                    foreach (var path in Plugin.LibraryApi.GetDeletePaths(mountPath)
                                 .Where(p => !p.IsDirectory &&
                                             (authorizedDeleteScopes.Count == 0 ||
                                              authorizedDeleteScopes.Any(scope =>
                                                  PathBoundary.IsLocalFileInScope(p.FullName, scope)))))
                    {
                        deleteFiles.Add(path.FullName);
                    }
                }
            }

            var transaction = VersionDeleteTransaction.Execute(deleteFiles, metadataDirectories,
                _fileSystem.MoveFile, _fileSystem.MoveDirectory,
                () => _itemRepository.DeleteItems(deleteItems.ToArray()),
                path => _fileSystem.DeleteDirectory(path, true));
            if (!transaction.Committed)
            {
                _logger.Error("DeleteVersion - transaction rolled back: " + transaction.Error);
                return new DeleteVersionResult
                {
                    Status = "RolledBack",
                    WouldDeleteCount = deleteItems.Count,
                    RetainedStagingCount = transaction.RetainedStagingPaths.Count,
                    Error = "Delete transaction failed; check server logs."
                };
            }

            if (transaction.RetainedStagingPaths.Count > 0)
            {
                _logger.Warn("DeleteVersion - committed with retained staging directories: " +
                             string.Join(" | ", transaction.RetainedStagingPaths));
            }

            if (enableNotification && allMountPaths.Count > 0 && user != null)
            {
                _ = BackgroundTaskRunner.Run("DeepDelete.Notify", () => Plugin.NotificationApi.DeepDeleteSendNotification(item, user,
                    new HashSet<string>(allMountPaths.Keys)));
            }

            return new DeleteVersionResult
            {
                Status = "Deleted",
                WouldDeleteCount = deleteItems.Count,
                DeletedCount = deleteItems.Count,
                RetainedStagingCount = transaction.RetainedStagingPaths.Count
            };
        }

        private string[] GetDeepDeleteScope(BaseItem item)
        {
            var options = _libraryManager.GetLibraryOptions(item);
            var paths = options?.PathInfos?
                .Select(p => p.Path)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
            if (paths != null && paths.Length > 0) return paths;

            var folder = _fileSystem.GetDirectoryName(item.Path);
            return string.IsNullOrEmpty(folder) ? Array.Empty<string>() : new[] { folder };
        }

        /// <summary>
        /// 只读预览：列出「删除整季该版本」会删掉哪些条目、哪些候选因版本身份不明被跳过。
        /// 不执行任何删除。权限判断与删除入口保持一致，避免向无删除权限的用户泄露路径。
        /// </summary>
        public object Any(DeleteVersionPreview request)
        {
            var result = new DeleteVersionPreviewResult
            {
                DeleteParent = request?.DeleteParent ?? false
            };

            if (!Plugin.IsPluginEnabled)
            {
                result.Message = "Plugin is disabled";
                return result;
            }

            var item = _libraryManager.GetItemById(request.Id);
            if (!(item is Video video) || !(video is Movie || video is Episode) || !video.IsFileProtocol ||
                video.GetAlternateVersionIds().Count == 0)
            {
                result.Message = "Only movies and episodes with alternate versions are supported";
                return result;
            }

            var user = GetUserForRequest(null);
            bool CanDeleteItem(BaseItem target)
            {
                var folders = _libraryManager.GetCollectionFolders(target);
                return user == null ? target.CanDelete() : target.CanDelete(user, folders);
            }

            var candidates = request.DeleteParent && item is Episode episode
                ? GetSeasonEpisodesSameVersion(episode, result.Skipped)
                : new List<BaseItem> { item };
            var deletePlan = VersionDeletePlanner.Build(item, candidates, CanDeleteItem);
            if (deletePlan.IsDenied)
            {
                result.Message = "No delete permission";
                return result;
            }

            result.ItemId = item.Id.ToString();
            foreach (var target in deletePlan.Authorized)
            {
                result.WillDelete.Add(new DeleteVersionPreviewEntry
                {
                    Id = target.Id.ToString(),
                    Name = target.Name
                });
            }

            result.SkippedCount = result.Skipped.Count + deletePlan.UnauthorizedCount;
            result.Message = $"Will delete {result.WillDelete.Count} item(s); skipped {result.SkippedCount} candidate(s)";
            return result;
        }

        private List<BaseItem> GetSeasonEpisodesSameVersion(Episode episode,
            List<DeleteVersionPreviewEntry> skipped = null)
        {
            var seasonFolderChildren = episode.Parent.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { nameof(Episode) },
                    Recursive = false,
                    GroupByPresentationUniqueKey = false,
                })
                .ToList();

            var seasonEpisodesCount = episode.Season.GetEpisodeIds(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { nameof(Episode) }, GroupByPresentationUniqueKey = true
                })
                .Length;

            if (seasonFolderChildren.Count == seasonEpisodesCount)
            {
                return seasonFolderChildren;
            }

            var targetCleaned = CleanEpisodeName(episode.FileNameWithoutExtension);

            var allEpisodes = episode.Season
                .GetEpisodes(new InternalItemsQuery
                {
                    GroupByPresentationUniqueKey = false, EnableTotalRecordCount = false
                })
                .Items.ToList();

            var similarEpisodes = new List<BaseItem>();

            foreach (var ep in allEpisodes)
            {
                var cleanedName = CleanEpisodeName(ep.FileNameWithoutExtension);
                var similarity = LevenshteinDistance(targetCleaned, cleanedName);

                if (similarity <= 0.92) continue;

                // 名称相似度不足以证明版本身份：1080p 与 2160p 在长文件名里的相似度可达 0.94+，
                // 仅凭相似度会把两个版本判成同一版，进而删掉另一版本的真实文件。
                // 删除不可逆，判不明一律排除（宁可少删）。
                if (!VersionIdentity.IsSameVersion(episode.Path, episode.FileNameWithoutExtension,
                    ep.Path, ep.FileNameWithoutExtension, out var reason))
                {
                    _logger.Info(
                        $"DeleteVersion - Skip candidate (version identity not confirmed: {reason}): {ep.Name} - {ep.Path}");

                    skipped?.Add(new DeleteVersionPreviewEntry
                    {
                        Reason = reason
                    });

                    continue;
                }

                similarEpisodes.Add(ep);
            }

            return similarEpisodes;
        }
    }

    /// <summary>删除预览结果。只读，不含任何副作用。</summary>
    public class DeleteVersionPreviewResult
    {
        public string ItemId { get; set; }
        public bool DeleteParent { get; set; }
        public string Message { get; set; }
        public List<DeleteVersionPreviewEntry> WillDelete { get; set; } = new List<DeleteVersionPreviewEntry>();
        public List<DeleteVersionPreviewEntry> Skipped { get; set; } = new List<DeleteVersionPreviewEntry>();
        public int SkippedCount { get; set; }
    }

    /// <summary>预览中的单个条目；<see cref="Reason"/> 仅在被跳过时有值。</summary>
    public class DeleteVersionPreviewEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Reason { get; set; }
    }

}
