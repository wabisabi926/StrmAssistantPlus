using StrmAssistant.Properties;
using StrmAssistant.Web.Service;
using Xunit;

namespace StrmAssistant.Tests
{
    public class MetadataCacheVerifyServiceTests
    {
        [Fact]
        public void BuildSummary_UsesFullCountsInsteadOfSampleCounts()
        {
            var response = new MetadataCacheVerifyService.MetadataCacheVerifyResponse
            {
                Enabled = true,
                Cache = new MetadataCacheVerifyService.ScopeInfo
                {
                    Files = 10,
                    ExpiredFiles = 1,
                    OrphanCount = 73,
                    Orphans = new System.Collections.Generic.List<string>(new string[50])
                },
                Override = new MetadataCacheVerifyService.ScopeInfo
                {
                    Files = 2,
                    OrphanCount = 4
                },
                Patches = new MetadataCacheVerifyService.ScopeInfo
                {
                    Files = 3
                },
                MissingMovieCacheCount = 61,
                MissingSeriesCacheCount = 7,
                MissingMovieCache = new System.Collections.Generic.List<string>(new string[50]),
                MissingSeriesCache = new System.Collections.Generic.List<string>(new string[7])
            };

            var summary = MetadataCacheVerifyService.BuildSummary(response);
            var expected = string.Format(Resources.GeneralOptions_VerifyMetadataCache_Result,
                10, 1, 2, 3, 77, 68);

            Assert.Equal(expected, summary);
        }
    }
}
