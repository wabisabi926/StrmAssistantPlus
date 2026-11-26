using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using StrmAssistant.Mod;
using System;
using System.Reflection;
using Xunit;

namespace StrmAssistant.Tests
{
    public class MetadataNullSafetyTests
    {
        [Fact]
        public void ChineseTvdbPostfix_AllowsMissingMetadataLanguage()
        {
            var lookup = new ItemLookupInfo();
            var languages = new[] { "eng" };

            Invoke(typeof(ChineseTvdb), "ConvertToTvdbLanguagesPostfix", lookup, languages);

            Assert.Equal(new[] { "eng" }, languages);
        }

        [Fact]
        public void ChineseTvdbPostfix_AllowsMissingLanguageResult()
        {
            var lookup = new ItemLookupInfo { MetadataLanguage = "zh-CN" };

            Invoke(typeof(ChineseTvdb), "ConvertToTvdbLanguagesPostfix", lookup, null);
        }

        [Fact]
        public void BeautifyDtosPostfix_AllowsMissingInputs()
        {
            Invoke(typeof(BeautifyMissingMetadata), "GetBaseItemDtosPostfix", null, null);
            Invoke(typeof(BeautifyMissingMetadata), "GetBaseItemDtosPostfix", new BaseItem[] { new Video() }, null);
        }

        [Fact]
        public void ShouldBeautifyEpisode_AllowsMissingPreferredMetadataLanguage()
        {
            var dto = new BaseItemDto { Name = "Show.S01E01" };

            Assert.False(BeautifyMissingMetadata.ShouldBeautifyEpisode(1, "Show.S01E01", "Show.S01E01", null, dto));
        }

        [Fact]
        public void ShouldBeautifyEpisode_AllowsMissingDto()
        {
            Assert.False(BeautifyMissingMetadata.ShouldBeautifyEpisode(1, "Show.S01E01", "Show.S01E01", "zh-CN", null));
        }

        [Fact]
        public void ShouldBeautifyEpisode_PreservesChineseEpisodeBehavior()
        {
            var dto = new BaseItemDto { Name = "Show.S01E01" };

            Assert.True(BeautifyMissingMetadata.ShouldBeautifyEpisode(1, "Show.S01E01", "Show.S01E01", "zh-CN", dto));
        }

        [Fact]
        public void ShouldBeautifyEpisode_RejectsNonChineseEpisode()
        {
            var dto = new BaseItemDto { Name = "Show.S01E01" };

            Assert.False(BeautifyMissingMetadata.ShouldBeautifyEpisode(1, "Show.S01E01", "Show.S01E01", "en-US", dto));
        }

        private static void Invoke(Type type, string methodName, params object[] args)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            method.Invoke(null, args);
        }
    }
}
