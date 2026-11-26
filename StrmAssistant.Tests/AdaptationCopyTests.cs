using StrmAssistant.Properties;
using System.Globalization;
using Xunit;

namespace StrmAssistant.Tests
{
    public class AdaptationCopyTests
    {
        private const string FingerprintKey =
            "ExtractIntroFingerprintTask_Description_Extracts_intro_fingerprint_from_episodes";
        private const string BoxSetsKey =
            "UIFunctionOptions_NoBoxsetsAutoCreation_Prevent_automatic_boxset_library_creation__Default_is_OFF_";
        private const string FolderLimitKey =
            "ExperienceEnhanceOptions_FolderVideoGroupingLimit_Maximum_number_of_videos_in_the_same_folder_eligible_for_multi_version_merge__8_is_the_Emby_default__Default_is_8_";
        private const string IntroScopeKey =
            "IntroSkipOptions_UnlockIntroSkip_Unlock_Strm_support_for_built_in_intro_skip_detection";

        [Theory]
        [InlineData("zh-CN", FingerprintKey, "当前 Emby 已提供原生片头检测时，此任务不执行")]
        [InlineData("zh-Hant", FingerprintKey, "目前 Emby 已提供原生片頭偵測時，此工作不執行")]
        [InlineData("en-US", FingerprintKey, "does not run when Emby provides native intro detection")]
        [InlineData("zh-CN", BoxSetsKey, "从用户视图隐藏合集媒体库，并阻止可拦截的自动创建")]
        [InlineData("zh-Hant", BoxSetsKey, "從使用者檢視隱藏合集媒體庫，並阻止可攔截的自動建立")]
        [InlineData("en-US", BoxSetsKey, "blocks auto-creation where Emby allows interception")]
        [InlineData("zh-CN", FolderLimitKey, "Emby 默认值为 8")]
        [InlineData("zh-Hant", FolderLimitKey, "Emby 預設值為 8")]
        [InlineData("en-US", FolderLimitKey, "The Emby default is 8")]
        [InlineData("zh-CN", IntroScopeKey, "启用片头检测范围控制并支持 STRM")]
        [InlineData("zh-Hant", IntroScopeKey, "啟用片頭偵測範圍控制並支援 STRM")]
        [InlineData("en-US", IntroScopeKey, "intro-detection scope controls and STRM support")]
        public void AdaptationCopy_UsesCurrentCapabilityWording(string cultureName, string key, string expected)
        {
            var value = Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(cultureName));

            Assert.Contains(expected, value);
        }

        [Theory]
        [InlineData("zh-CN", "MetadataEnhanceOptions_CommunityRatingSource_Description", "选择 TMDb 或 IMDb 时由本插件写入")]
        [InlineData("zh-Hant", "MetadataEnhanceOptions_CommunityRatingSource_Description", "選擇 TMDb 或 IMDb 時由本外掛寫入")]
        [InlineData("en-US", "MetadataEnhanceOptions_CommunityRatingSource_Description", "The plugin writes the selected TMDb or IMDb rating")]
        [InlineData("zh-CN", "ModOptions_ProxyServer_Enable_http_proxy_server__Blank_is_OFF_", "通过连通性测试后启用")]
        [InlineData("zh-Hant", "ModOptions_ProxyServer_Enable_http_proxy_server__Blank_is_OFF_", "通過連線測試後啟用")]
        [InlineData("en-US", "ModOptions_ProxyServer_Enable_http_proxy_server__Blank_is_OFF_", "successful connectivity test")]
        [InlineData("zh-CN", "NetworkOptions_ProxyMode_Global_proxies_all_outbound_requests__ScrapeOnly_proxies_only_the_scraping_whitelist_hosts__Default_is_Global_", "回环地址和私网 IPv4 直连")]
        [InlineData("zh-Hant", "NetworkOptions_ProxyMode_Global_proxies_all_outbound_requests__ScrapeOnly_proxies_only_the_scraping_whitelist_hosts__Default_is_Global_", "回環位址和私網 IPv4 直連")]
        [InlineData("en-US", "NetworkOptions_ProxyMode_Global_proxies_all_outbound_requests__ScrapeOnly_proxies_only_the_scraping_whitelist_hosts__Default_is_Global_", "loopback and private IPv4 addresses connect directly")]
        [InlineData("zh-CN", "MediaInfoExtractOptions_ClearEmbeddedMetadata_Strip_embedded_metadata_from_movie_and_episode_probe_results__Default_is_ON_", "仅独占提取模块生效")]
        [InlineData("zh-Hant", "MediaInfoExtractOptions_ClearEmbeddedMetadata_Strip_embedded_metadata_from_movie_and_episode_probe_results__Default_is_ON_", "僅獨佔提取模組生效")]
        [InlineData("en-US", "MediaInfoExtractOptions_ClearEmbeddedMetadata_Strip_embedded_metadata_from_movie_and_episode_probe_results__Default_is_ON_", "applies only when Exclusive Extract is active")]
        [InlineData("zh-CN", "MetadataEnhanceOptions_DoubanPosterFallback_Description", "首选元数据语言为中文")]
        [InlineData("zh-Hant", "MetadataEnhanceOptions_DoubanPosterFallback_Description", "偏好的元資料語言為中文")]
        [InlineData("en-US", "MetadataEnhanceOptions_DoubanPosterFallback_Description", "preferred metadata language is Chinese")]
        [InlineData("zh-CN", "IntroSkipControl_ResetAndOverwrite_ResetAndOverwrite", "重置时覆盖原生片头片尾标记")]
        [InlineData("zh-Hant", "IntroSkipControl_ResetAndOverwrite_ResetAndOverwrite", "重設時覆蓋原生片頭片尾標記")]
        [InlineData("en-US", "IntroSkipControl_ResetAndOverwrite_ResetAndOverwrite", "Reset overwrites native intro and credits markers")]
        [InlineData("zh-CN", "MediaInfoMenu_PluginDisabled", "插件已停用")]
        [InlineData("zh-Hant", "MediaInfoMenu_PluginDisabled", "外掛已停用")]
        [InlineData("en-US", "MediaInfoMenu_PluginDisabled", "The plugin is disabled")]
        [InlineData("zh-CN", "MediaInfoMenu_NoSupportedItems", "没有可提取媒体信息的电影或单集")]
        [InlineData("zh-Hant", "MediaInfoMenu_NoSupportedItems", "沒有可擷取媒體資訊的電影或單集")]
        [InlineData("en-US", "MediaInfoMenu_NoSupportedItems", "no movies or episodes that support media info extraction")]
        [InlineData("zh-CN", "MediaInfoMenu_DryRun", "未删除任何媒体信息")]
        [InlineData("zh-Hant", "MediaInfoMenu_DryRun", "未刪除任何媒體資訊")]
        [InlineData("en-US", "MediaInfoMenu_DryRun", "no media info was deleted")]
        public void CorrectedCopy_UsesVerifiedCapabilityWording(string cultureName, string key, string expected)
        {
            var value = Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(cultureName));

            Assert.Contains(expected, value);
        }

        [Theory]
        [InlineData("zh-CN", FolderLimitKey)]
        [InlineData("zh-Hant", FolderLimitKey)]
        [InlineData("en-US", FolderLimitKey)]
        public void FolderLimitCopy_DoesNotRepeatDefault(string cultureName, string key)
        {
            var value = Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(cultureName));

            Assert.DoesNotContain("默认为 8。默认为 8", value);
            Assert.DoesNotContain("預設為 8。預設為 8", value);
            Assert.DoesNotContain("Default is 8. Default is 8", value);
        }
    }
}
