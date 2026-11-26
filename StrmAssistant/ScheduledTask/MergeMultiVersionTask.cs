using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Common;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static StrmAssistant.Options.VersionMergeOptions;

namespace StrmAssistant.ScheduledTask
{
    public class TriggerMergeMovieTask : ILibraryPostScanTask
    {
        private readonly MergeMultiVersionTask _task;
        
        public TriggerMergeMovieTask(MergeMultiVersionTask task) => _task = task;

        public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Plugin.Instance.ExperienceEnhanceStore.GetOptions().Merge.MergeMultiVersion
                ? _task.Execute(cancellationToken, progress)
                : Task.CompletedTask;
        }
    }

    public class MergeMultiVersionTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;

        private static readonly HashSet<string> ProviderIdCheckKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tmdb", "imdb", "tvdb" };

        public static readonly AsyncLocal<CollectionFolder> CurrentScanLibrary = new AsyncLocal<CollectionFolder>();

        public MergeMultiVersionTask(ILibraryManager libraryManager, IProviderManager providerManager)
        {
            _logger = Plugin.Instance.Logger;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            if (!Plugin.IsPluginEnabled) return;

            _logger.Info("MergeMultiVersion - Scheduled Task Execute");

            if (DryRunMode.Skip("merge multi-version", _logger))
            {
                progress.Report(100);
                return;
            }

            // 库选项可能在上次运行后被改动（界面切换/自动分组被改写），先清缓存
            Mod.MergeMultiVersion.ClearSeriesGroupingCache();

            var currentScanLibrary = CurrentScanLibrary.Value;
            CurrentScanLibrary.Value = null;

            double cumulativeProgress = 0;

            var seriesLibraryGroups = PrepareMergeSeries();
            var movieLibraryGroups = PrepareMergeMovies(currentScanLibrary);

            var processSeries = seriesLibraryGroups.Any() && (currentScanLibrary is null ||
                                                              string.Equals(currentScanLibrary.CollectionType, CollectionType.TvShows.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                                              currentScanLibrary.CollectionType is null);
            var processMovies = movieLibraryGroups.Any() && (currentScanLibrary is null ||
                                                             string.Equals(currentScanLibrary.CollectionType, CollectionType.Movies.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                                             currentScanLibrary.CollectionType is null);

            if (processSeries)
            {
                var multiply = processMovies ? 1 : 2;

                var alternativeSeries = FindDuplicateSeries(seriesLibraryGroups);
                progress.Report(cumulativeProgress += 5.0 * multiply);

                if (alternativeSeries.Any())
                {
                    var seriesProgressWeight = 35.0 * multiply / alternativeSeries.Count;

                    foreach (var series in alternativeSeries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (currentScanLibrary is null || _libraryManager.GetCollectionFolders(series)
                                .Any(c => c.InternalId != currentScanLibrary.InternalId))
                        {
                            var refreshOptions = Plugin.MetadataApi.GetMetadataValidationRefreshOptions();
                            Traverse.Create(refreshOptions).Property("Recursive").SetValue(true);

                            await _providerManager.RefreshFullItem(series, refreshOptions, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        cumulativeProgress += seriesProgressWeight;
                        progress.Report(cumulativeProgress);

                        _logger.Info($"MergeMultiVersion - Series merged: {series.Name} - {series.Path}");
                    }
                }
                else
                {
                    cumulativeProgress += 35.0 * multiply;
                    progress.Report(cumulativeProgress);
                }

                var inconsistentSeries = FindInconsistentSeries(seriesLibraryGroups);
                progress.Report(cumulativeProgress += 5.0 * multiply);

                if (inconsistentSeries.Any())
                {
                    var seriesProgressWeight = 5.0 * multiply / inconsistentSeries.Count;

                    foreach (var series in inconsistentSeries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var refreshOptions = Plugin.MetadataApi.GetMetadataValidationRefreshOptions();
                        Traverse.Create(refreshOptions).Property("Recursive").SetValue(true);

                        await _providerManager.RefreshFullItem(series, refreshOptions, cancellationToken)
                            .ConfigureAwait(false);

                        cumulativeProgress += seriesProgressWeight;
                        progress.Report(cumulativeProgress);

                        _logger.Info($"MergeMultiVersion - Series merged: {series.Name} - {series.Path}");
                    }
                }
                else
                {
                    cumulativeProgress += 5.0 * multiply;
                    progress.Report(cumulativeProgress);
                }
            }

            if (processMovies)
            {
                var totalGroups = movieLibraryGroups.Length;
                var groupProgressWeight = processSeries ? 50.0 / totalGroups : 100.0 / totalGroups;

                foreach (var group in movieLibraryGroups)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var groupProgress = new Progress<double>(p =>
                    {
                        cumulativeProgress += p * groupProgressWeight / 100;
                        progress.Report(cumulativeProgress);
                    });

                    ExecuteMergeMovies(group, groupProgress);
                }
            }

            progress.Report(100);
            _logger.Info("MergeMultiVersion - Scheduled Task Complete");
        }

        public string Category => Resources.ResourceManager.GetString("ScheduledTask_Category_MultiVersion",
            Plugin.Instance.DefaultUICulture);

        public string Key => "MergeMultiVersionTask";

        public string Description => Resources.ResourceManager.GetString(
            "MergeMovieTask_Description_Merge_movies_per_library_or_across_libraries_per_preference",
            Plugin.Instance.DefaultUICulture);

        public string Name =>
            Resources.ResourceManager.GetString("MergeMultiVersionTask_Name_Merge_Multi_Versions",
                Plugin.Instance.DefaultUICulture);

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        public bool IsHidden => false;

        public bool IsEnabled => true;
        
        public bool IsLogged => true;

        private long[] PrepareMergeSeries()
        {
            var mergeSeriesPreference = Plugin.Instance.ExperienceEnhanceStore.GetOptions().Merge.MergeSeriesPreference;
            _logger.Info("MergeMultiVersion - Merge Series Preference: " + mergeSeriesPreference);

            if (!Plugin.Instance.IsModSupported || mergeSeriesPreference != MergeSeriesScopeOption.GlobalScope)
                return Array.Empty<long>();

            var libraries = Plugin.LibraryApi.GetSeriesLibraries()
                .Where(l => l.GetLibraryOptions().EnableAutomaticSeriesGrouping)
                .ToList();

            if (!libraries.Any()) return Array.Empty<long>();

            _logger.Info("MergeMultiVersion - Series Libraries: " + string.Join(", ", libraries.Select(l => l.Name)));

            var libraryGroups = libraries.Select(l => l.InternalId).ToArray();

            return libraryGroups;
        }

        private List<BaseItem> FindDuplicateSeries(long[] parents)
        {
            var allSeries = parents.SelectMany(parent =>
            {
                var query = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] { nameof(Series) },
                    HasAnyProviderId = new[]
                    {
                        MetadataProviders.Tmdb.ToString(), MetadataProviders.Imdb.ToString(),
                        MetadataProviders.Tvdb.ToString()
                    }
                };
                StrmAssistant.Common.LibraryApi.SetSingleParent(query, parent);
                return _libraryManager.GetItemList(query);
            }).ToList();

            var dupSeries = allSeries
                .SelectMany(item =>
                    item.ProviderIds.Where(kvp => ProviderIdCheckKeys.Contains(kvp.Key) &&
                                                   !string.IsNullOrWhiteSpace(kvp.Value))
                        .Select(kvp => new { kvp.Key, kvp.Value, item }))
                .GroupBy(x => new { x.Key, x.Value })
                .Where(g => g.Select(x => x.item.InternalId).Distinct().Count() > 1)
                .SelectMany(g => g.Select(x => x.item))
                .GroupBy(item => item.InternalId)
                .Select(g => g.First())
                .ToList();

            return dupSeries;
        }

        private List<Series> FindInconsistentSeries(long[] parents)
        {
            var allSeries = parents.SelectMany(parent =>
            {
                var query = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] { nameof(Season), nameof(Episode) },
                    GroupBySeriesPresentationUniqueKey = true
                };
                StrmAssistant.Common.LibraryApi.SetSingleParent(query, parent);
                return _libraryManager.GetItemList(query);
            }).ToList();

            var inconsistentSeries = allSeries
                .Select(item => new
                {
                    Series = (item as Season)?.Series ?? (item as Episode)?.Series, item.SeriesPresentationUniqueKey
                })
                .Where(x => x.Series != null && x.SeriesPresentationUniqueKey != x.Series.PresentationUniqueKey)
                .GroupBy(x => x.Series.InternalId)
                .Select(g => g.First().Series)
                .ToList();

            allSeries.Clear();
            allSeries.TrimExcess();

            return inconsistentSeries;
        }

        private long[][] PrepareMergeMovies(CollectionFolder currentScanLibrary)
        {
            var mergeMoviesPreference = Plugin.Instance.ExperienceEnhanceStore.GetOptions().Merge.MergeMoviesPreference;
            _logger.Info("MergeMultiVersion - Merge Movies Preference: " + mergeMoviesPreference);

            var libraryGroups = Array.Empty<long[]>();

            if (mergeMoviesPreference == MergeMoviesScopeOption.FolderScope) return libraryGroups;

            if (mergeMoviesPreference == MergeMoviesScopeOption.LibraryScope && currentScanLibrary != null)
            {
                libraryGroups = new[] { new[] { currentScanLibrary.InternalId } };
                _logger.Info("MergeMultiVersion - Movies Libraries: " + currentScanLibrary.Name);
            }
            else
            {
                var libraries = Plugin.LibraryApi.GetMovieLibraries();

                if (!libraries.Any()) return libraryGroups;

                _logger.Info(
                    "MergeMultiVersion - Movies Libraries: " + string.Join(", ", libraries.Select(l => l.Name)));

                var libraryIds = libraries.Select(l => l.InternalId).ToArray();
                libraryGroups = mergeMoviesPreference == MergeMoviesScopeOption.GlobalScope
                    ? new[] { libraryIds }
                    : libraryIds.Select(library => new[] { library }).ToArray();
            }

            return libraryGroups;
        }

        private void ExecuteMergeMovies(long[] parents, IProgress<double> groupProgress = null)
        {
            var allMovies = parents.SelectMany(parent =>
            {
                var query = new InternalItemsQuery
                {
                    Recursive = true,
                    IncludeItemTypes = new[] { nameof(Movie) },
                    HasAnyProviderId = new[]
                    {
                        MetadataProviders.Tmdb.ToString(),
                        MetadataProviders.Imdb.ToString(),
                        MetadataProviders.Tvdb.ToString()
                    }
                };
                StrmAssistant.Common.LibraryApi.SetSingleParent(query, parent);
                return _libraryManager.GetItemList(query);
            }).Cast<Movie>().ToList();

            var plan = VersionMergePlanner.Plan(allMovies, movie => movie.InternalId,
                movie => movie.ProviderIds);
            _logger.Info($"MergeMultiVersion - Movie candidates: {allMovies.Count}, merge groups: {plan.Groups.Count}, conflicts: {plan.Conflicts.Count}");

            foreach (var conflict in plan.Conflicts)
            {
                _logger.Warn($"MergeMultiVersion - Skipped conflicting {conflict.ProviderKey} group: {string.Join(",", conflict.Values)}; items: {string.Join(",", conflict.Items.Select(item => item.InternalId))}");
            }

            allMovies.Clear();
            allMovies.TrimExcess();

            if (plan.Groups.Count > 0)
            {
                var total = plan.Groups.Count;
                var current = 0;

                foreach (var group in plan.Groups)
                {
                    var movies = group.OfType<BaseItem>().ToArray();
                    _libraryManager.MergeItems(movies);

                    foreach (var item in movies)
                    {
                        _logger.Info($"MergeMultiVersion - Movie merged: {item.Name} - {item.Path}");
                    }

                    current++;
                    _logger.Info($"MergeMultiVersion - Merged group {current} of {total} with {movies.Length} items");

                    var progress = (double)current / total * 100;
                    groupProgress?.Report(progress);
                }

                groupProgress?.Report(100);
            }
        }
    }
}
