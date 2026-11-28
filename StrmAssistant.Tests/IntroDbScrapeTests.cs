using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using StrmAssistant.Common;
using StrmAssistant.ScheduledTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StrmAssistant.Tests
{
    public class IntroDbScrapeTests
    {
        private static List<IntroDbApi.IntroDbSegment> Seg(long? start, long? end) =>
            new List<IntroDbApi.IntroDbSegment> { new IntroDbApi.IntroDbSegment { start_ms = start, end_ms = end } };

        private static IntroDbApi.SourceResult Source(string provider, bool intro, bool credits) => new IntroDbApi.SourceResult
        {
            Provider = provider,
            Intro = intro ? Seg(1000, 2000) : null,
            Credits = credits ? Seg(3000, 4000) : null
        };

        [Theory]
        [InlineData("{\"error\":\"No submissions for this episode\",\"code\":\"no_submissions\"}", "no_submissions")]
        [InlineData("{\"error\":\"Episode not found\",\"code\":\"episode_not_found\",\"details\":\"S1E2\"}", "episode_not_found")]
        [InlineData("{\"error\":\"Media not found\",\"code\":\"media_not_found\"}", "media_not_found")]
        [InlineData("{\"code\":\" NO_SUBMISSIONS \"}", "no_submissions")]
        [InlineData("{\"code\":\"specific_media_usage_limit_exceeded\"}", "specific_media_usage_limit_exceeded")]
        public void ParseNoDataCode_ReadsTopLevelCode(string body, string expected)
        {
            Assert.Equal(expected, IntroDbApi.ParseNoDataCode(body));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Not Found")]
        [InlineData("{}")]
        [InlineData("{\"error\":\"No submissions\"}")]
        [InlineData("{\"code\":123}")]
        [InlineData("{\"error\":{\"code\":\"no_submissions\"}}")]
        [InlineData("[1,2]")]
        public void ParseNoDataCode_ReturnsNullWhenNoUsableCode(string body)
        {
            Assert.Null(IntroDbApi.ParseNoDataCode(body));
        }

        [Fact]
        public void MovieQuery_AppliesScrapeLibraryScope()
        {
            var query = ScrapeIntroDbTask.CreateMovieQuery(new[] { @"D:\MoviesA", @"D:\MoviesB" });

            Assert.Equal(new[] { @"D:\MoviesA", @"D:\MoviesB" }, query.PathStartsWithAny);
        }

        [Fact]
        public void MovieQuery_NoScopeKeepsAllMovies()
        {
            var query = ScrapeIntroDbTask.CreateMovieQuery(Array.Empty<string>());

            Assert.Empty(query.PathStartsWithAny);
        }

        [Fact]
        public void ResolveResults_PrefersCompleteSourceOverMerging()
        {
            var results = new List<IntroDbApi.SourceResult>
            {
                Source("IntroDb", false, true),
                Source("Tidb", true, true)
            };

            var merged = IntroDbApi.ResolveResults(results, out var introSource, out var creditsSource);

            Assert.Equal("Tidb", introSource);
            Assert.Equal("Tidb", creditsSource);
            Assert.Same(results[1].Intro, merged.intro);
            Assert.Same(results[1].Credits, merged.credits);
        }

        [Fact]
        public void ResolveResults_FirstCompleteSourceWins()
        {
            var results = new List<IntroDbApi.SourceResult>
            {
                Source("IntroDb", true, true),
                Source("Tidb", true, true)
            };

            var merged = IntroDbApi.ResolveResults(results, out var introSource, out var creditsSource);

            Assert.Equal("IntroDb", introSource);
            Assert.Equal("IntroDb", creditsSource);
            Assert.Same(results[0].Intro, merged.intro);
        }

        [Fact]
        public void ResolveResults_MergesWhenNoSourceComplete()
        {
            var results = new List<IntroDbApi.SourceResult>
            {
                Source("IntroDb", false, true),
                Source("Tidb", true, false)
            };

            var merged = IntroDbApi.ResolveResults(results, out var introSource, out var creditsSource);

            Assert.Equal("Tidb", introSource);
            Assert.Equal("IntroDb", creditsSource);
            Assert.Same(results[1].Intro, merged.intro);
            Assert.Same(results[0].Credits, merged.credits);
        }

        [Fact]
        public void ResolveResults_ReturnsNullWhenNoData()
        {
            var results = new List<IntroDbApi.SourceResult> { Source("IntroDb", false, false) };

            var merged = IntroDbApi.ResolveResults(results, out var introSource, out var creditsSource);

            Assert.Null(merged);
            Assert.Null(introSource);
            Assert.Null(creditsSource);
        }

        [Fact]
        public void MaxStartDeltaMs_ReportsLargestFieldDelta()
        {
            var results = new List<IntroDbApi.SourceResult>
            {
                new IntroDbApi.SourceResult { Provider = "IntroDb", Intro = Seg(0, 47000), Credits = Seg(1360000, null) },
                new IntroDbApi.SourceResult { Provider = "Tidb", Intro = Seg(500, 47500), Credits = Seg(2783000, null) }
            };

            Assert.Equal(1423000, IntroDbApi.MaxStartDeltaMs(results));
        }

        [Fact]
        public void OrderProviders_MoviePutsTidbFirst()
        {
            var ordered = IntroDbApi.OrderProviders(new List<string> { "IntroDb", "Tidb" }, true);

            Assert.Equal(new[] { "Tidb", "IntroDb" }, ordered);
        }

        [Fact]
        public void OrderProviders_TvKeepsConfiguredOrder()
        {
            var ordered = IntroDbApi.OrderProviders(new List<string> { "IntroDb", "Tidb" }, false);

            Assert.Equal(new[] { "IntroDb", "Tidb" }, ordered);
        }

        [Fact]
        public void ParseUnifiedProviders_KeepsExplicitSingleSource()
        {
            var providers = IntroDbApi.ParseUnifiedProviders("IntroDb");

            Assert.Single(providers);
            Assert.Equal("IntroDb", providers[0]);
        }

        [Fact]
        public void ParseUnifiedProviders_UsesDefaultWhenEmpty()
        {
            var providers = IntroDbApi.ParseUnifiedProviders(string.Empty);

            Assert.Equal(new[] { "IntroDb", "Tidb" }, providers);
        }

        [Fact]
        public void BuildTheIntroDbUrl_KeepsTmdbAndAddsTvdbEpisodeIdAsPin()
        {
            // v4 要求媒体 id + season/episode；单集 TVDB id 只能作为附加提示，不能单独使用。
            var url = IntroDbApi.BuildTheIntroDbUrl("1399", "121361", "5577121", "tt0944947", 1, 2, 3_600_000);

            Assert.Equal(
                "https://api.theintrodb.org/v4/media?tmdb_id=1399&season=1&episode=2&tvdb_episode_id=5577121&duration_ms=3600000",
                url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_MovieIgnoresTvdbEpisodeId()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl("603", "169", "5577121", "tt0133093", null, null, null);

            Assert.DoesNotContain("tvdb_episode_id", url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_FallsBackToTmdb()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl("1399", "121361", null, "tt0944947", 1, 2, null);

            Assert.Equal(
                "https://api.theintrodb.org/v4/media?tmdb_id=1399&season=1&episode=2",
                url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_FallsBackToTvdbWhenNoTmdb()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl(null, "121361", null, "tt0944947", 1, 2, null);

            Assert.Equal(
                "https://api.theintrodb.org/v4/media?tvdb_id=121361&season=1&episode=2",
                url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_FallsBackToImdb()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl(null, null, null, "tt0944947", 1, 2, null);

            Assert.Equal(
                "https://api.theintrodb.org/v4/media?imdb_id=tt0944947&season=1&episode=2",
                url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_MovieUsesTmdbWithoutType()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl("603", "169", null, "tt0133093", null, null, null);

            Assert.Equal("https://api.theintrodb.org/v4/media?tmdb_id=603", url);
            Assert.DoesNotContain("type=", url);
            Assert.DoesNotContain("season", url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_ReturnsNullWhenNoIdAvailable()
        {
            Assert.Null(IntroDbApi.BuildTheIntroDbUrl(null, null, null, null, 1, 2, null));
        }

        [Fact]
        public void BuildTheIntroDbUrl_OmitsNonPositiveDuration()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl("603", null, null, null, null, null, 0);

            Assert.DoesNotContain("duration_ms", url);
        }

        [Theory]
        [InlineData("default")]
        [InlineData("official")]
        [InlineData("dvd")]
        [InlineData("alternate")]
        [InlineData("absolute")]
        [InlineData("regional")]
        public void NormalizeTheIntroDbMapping_AcceptsSupportedValues(string mapping)
        {
            Assert.Equal(mapping, IntroDbApi.NormalizeTheIntroDbMapping(" " + mapping.ToUpperInvariant() + " "));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("unknown")]
        public void NormalizeTheIntroDbMapping_RejectsUnsupportedValues(string mapping)
        {
            Assert.Null(IntroDbApi.NormalizeTheIntroDbMapping(mapping));
        }

        [Theory]
        [InlineData("Aired", "official")]
        [InlineData("Dvd", "dvd")]
        [InlineData("Absolute", "absolute")]
        [InlineData("Unknown", null)]
        public void GetTheIntroDbMapping_MapsDisplayOrder(string displayOrder, string expected)
        {
            Assert.Equal(expected, IntroDbApi.GetTheIntroDbMapping(displayOrder));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("Tvdb-1_aired", "official")]
        [InlineData("Tvdb-1_official", "official")]
        [InlineData("tvdb-2_dvd", "dvd")]
        [InlineData("Tvdb-3_absolute", "absolute")]
        [InlineData("dvd", "dvd")]
        [InlineData("Tvdb-custom", null)]
        public void GetTheIntroDbMappingFromSeriesOrder_MapsEmby411Order(string seriesOrder, string expected)
        {
            Assert.Equal(expected, IntroDbApi.GetTheIntroDbMappingFromSeriesOrder(seriesOrder));
        }

        [Fact]
        public void GetTheIntroDbMapping_FallsBackToDisplayOrderWhenSeriesOrderIsUnavailable()
        {
            var series = new Series { DisplayOrder = SeriesDisplayOrder.Dvd };

            Assert.Equal("dvd", IntroDbApi.GetTheIntroDbMapping(series));
        }

        [Fact]
        public void BuildTheIntroDbUrl_AddsMappingOnlyForTvdbEpisodeWithoutEpisodePin()
        {
            var url = IntroDbApi.BuildTheIntroDbUrl(null, "121361", null, "tt0944947", 1, 2, null, "dvd");

            Assert.Equal(
                "https://api.theintrodb.org/v4/media?tvdb_id=121361&season=1&episode=2&mapping=dvd",
                url);
        }

        [Fact]
        public void BuildTheIntroDbUrl_OmitsMappingForTmdbAndImdbPaths()
        {
            var tmdb = IntroDbApi.BuildTheIntroDbUrl("1399", "121361", null, "tt0944947", 1, 2, null, "dvd");
            var imdb = IntroDbApi.BuildTheIntroDbUrl(null, null, null, "tt0944947", 1, 2, null, "dvd");

            Assert.DoesNotContain("mapping=", tmdb);
            Assert.DoesNotContain("mapping=", imdb);
        }

        [Fact]
        public void BuildTheIntroDbUrl_OmitsMappingWhenEpisodeIsPinnedOrMovie()
        {
            var pinned = IntroDbApi.BuildTheIntroDbUrl(null, "121361", "5577121", null, 1, 2, null, "dvd");
            var movie = IntroDbApi.BuildTheIntroDbUrl(null, "121361", null, null, null, null, null, "dvd");

            Assert.DoesNotContain("mapping=", pinned);
            Assert.DoesNotContain("mapping=", movie);
        }

        [Fact]
        public async Task SendTheIntroDbWithMappingFallback_RetriesOnceWithoutMappingOn400()
        {
            var statuses = new Queue<HttpStatusCode>(new[] { HttpStatusCode.BadRequest, HttpStatusCode.OK });
            var urls = new List<string>();
            Task<HttpResponseMessage> Send(HttpRequestMessage request)
            {
                urls.Add(request.RequestUri.AbsoluteUri);
                return Task.FromResult(new HttpResponseMessage(statuses.Dequeue()));
            }

            var result = await IntroDbApi.SendTheIntroDbWithMappingFallbackAsync(Send,
                "https://api.theintrodb.org/v4/media?tvdb_id=1&mapping=dvd",
                "https://api.theintrodb.org/v4/media?tvdb_id=1", null, null, CancellationToken.None);
            using (result.Response)
            {
                Assert.True(result.UsedFallback);
                Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
                Assert.Equal(2, urls.Count);
                Assert.Contains("mapping=dvd", urls[0]);
                Assert.DoesNotContain("mapping=", urls[1]);
            }
        }

        [Fact]
        public async Task SendTheIntroDbWithMappingFallback_DoesNotRetryWithoutFallbackUrl()
        {
            var statuses = new Queue<HttpStatusCode>(new[] { HttpStatusCode.BadRequest });
            var calls = 0;
            Task<HttpResponseMessage> Send(HttpRequestMessage request)
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(statuses.Dequeue()));
            }

            var result = await IntroDbApi.SendTheIntroDbWithMappingFallbackAsync(Send,
                "https://api.theintrodb.org/v4/media?tvdb_id=1&mapping=dvd", null, null, null,
                CancellationToken.None);
            using (result.Response)
            {
                Assert.False(result.UsedFallback);
                Assert.Equal(1, calls);
                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
            }
        }

        [Fact]
        public async Task SendTheIntroDbWithMappingFallback_StopsAfterSecond400()
        {
            var statuses = new Queue<HttpStatusCode>(new[] { HttpStatusCode.BadRequest, HttpStatusCode.BadRequest });
            var calls = 0;
            Task<HttpResponseMessage> Send(HttpRequestMessage request)
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(statuses.Dequeue()));
            }

            var result = await IntroDbApi.SendTheIntroDbWithMappingFallbackAsync(Send,
                "https://api.theintrodb.org/v4/media?tvdb_id=1&mapping=dvd",
                "https://api.theintrodb.org/v4/media?tvdb_id=1", null, null, CancellationToken.None);
            using (result.Response)
            {
                Assert.True(result.UsedFallback);
                Assert.Equal(2, calls);
                Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
            }
        }

        [Fact]
        public void TheIntroDbBaseUrl_IsV4()
        {
            Assert.EndsWith("/v4", IntroDbApi.TheIntroDbBaseUrl);
        }

        [Fact]
        public void PickLongestSegment_UsesEndOnlySegment()
        {
            var segments = new List<IntroDbApi.IntroDbSegment>
            {
                new IntroDbApi.IntroDbSegment { start_ms = 0, end_ms = 40000 },
                new IntroDbApi.IntroDbSegment { start_ms = null, end_ms = 90000 }
            };

            var picked = ChapterApi.PickLongestSegment(segments);

            Assert.NotNull(picked);
            Assert.Null(picked.start_ms);
            Assert.Equal(90000, picked.end_ms);
        }

        [Fact]
        public void PickLongestSegment_DropsSegmentsWithoutAnyBoundary()
        {
            var segments = new List<IntroDbApi.IntroDbSegment>
            {
                new IntroDbApi.IntroDbSegment()
            };

            Assert.Null(ChapterApi.PickLongestSegment(segments));
        }

        [Fact]
        public void GetIntroMarkerTicks_UsesRuntimeWhenEndMissing()
        {
            var segment = new IntroDbApi.IntroDbSegment { start_ms = 1000, end_ms = null };

            var (start, end) = ChapterApi.GetIntroMarkerTicks(segment, 1_000_000_000);

            Assert.Equal(10_000_000, start);
            Assert.Equal(1_000_000_000, end);
        }

        [Fact]
        public void GetIntroMarkerTicks_ClampsEndToRuntime()
        {
            var segment = new IntroDbApi.IntroDbSegment { start_ms = 0, end_ms = 60000 };

            var (_, end) = ChapterApi.GetIntroMarkerTicks(segment, 50_000_000);

            Assert.Equal(50_000_000, end);
        }

        [Fact]
        public void GetIntroMarkerTicks_MovieEndOnlyStartsAtZero()
        {
            var segment = new IntroDbApi.IntroDbSegment { start_ms = null, end_ms = 90000 };

            var (start, end) = ChapterApi.GetIntroMarkerTicks(segment, null);

            Assert.Equal(0, start);
            Assert.Equal(900_000_000, end);
        }
    }
}
