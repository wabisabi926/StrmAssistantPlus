using StrmAssistant.Common;
using StrmAssistant.ScheduledTask;
using System;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    public class IntroMarkerCleanupTests
    {
        [Theory]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void ShouldScrapeMovie_OnlySkipsWhenExternalIntroAndCreditsExist(
            bool hasNonDbIntro, bool hasCredits, bool expected)
        {
            Assert.Equal(expected, ScrapeIntroDbTask.ShouldScrapeMovie(hasNonDbIntro, hasCredits));
        }

        [Fact]
        public void ShouldIncludeAllClearLibraries_WhenAllScopesBlank()
        {
            Assert.True(ChapterApi.ShouldIncludeAllClearLibraries(new[] { string.Empty, "  ", null }));
        }

        [Fact]
        public void ClearTask_IncludesAllMarkerSources()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var source = File.ReadAllText(Path.Combine(root, "StrmAssistant/Common/ChapterApi.cs"));

            Assert.DoesNotContain("AllowOverwrite(c, !clearAll)", source);
            Assert.Contains("c.MarkerType == MarkerType.CreditsStart);", source);
        }

        [Theory]
        [InlineData("3", "", "")]
        [InlineData("", "7", "")]
        [InlineData("", "", "9")]
        [InlineData("3", "7", "9")]
        public void ShouldIncludeAllClearLibraries_WhenAnyScopeConfigured(
            string playback, string fingerprint, string scrape)
        {
            Assert.False(ChapterApi.ShouldIncludeAllClearLibraries(new[] { playback, fingerprint, scrape }));
        }
    }
}
