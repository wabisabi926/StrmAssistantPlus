using StrmAssistant.Properties;
using System.Globalization;
using Xunit;

namespace StrmAssistant.Tests
{
    public class ScheduledTaskCopyTests
    {
        public static TheoryData<string, string, string, string> CopyCases => new TheoryData<string, string, string, string>
        {
            { "ScheduledTask_Category_MediaInfo", "Media Info", "媒体信息", "媒體資訊" },
            { "ScheduledTask_Category_Metadata", "Metadata", "元数据", "元資料" },
            { "ScheduledTask_Category_IntroCredits", "Intro & Credits", "片头片尾", "片頭片尾" },
            { "ScheduledTask_Category_MultiVersion", "Multiple Versions", "多版本", "多版本" },
            { "ScheduledTask_Category_Maintenance", "Maintenance", "维护工具", "維護工具" },
            { "ScheduledTask_Category_PluginUpdate", "Plugin Update", "插件更新", "外掛更新" },
            { "CleanupMetadataCacheTask_Name_Cleanup_Metadata_Cache", "Clean Expired Metadata Cache", "清理过期元数据缓存", "清理過期元資料快取" },
            { "DeletePersonTask_Name_Delete_Persons", "Delete All People", "删除所有人物条目", "刪除所有人物項目" },
            { "MediaHealthCheckTask_Name_Media_Health_Check", "Check Media Info", "检查媒体信息", "檢查媒體資訊" },
            { "PersistMediaInfoTask_Name_Persist_MediaInfo", "Export Media Info", "导出媒体信息", "匯出媒體資訊" },
            { "RefreshEpisodeTask_Name_Refresh_Episode", "Refresh Episode Metadata", "刷新单集元数据", "重新整理單集元資料" },
            { "RefreshPersonTask_Name_Refresh_Chinese_Actor", "Refresh People", "刷新人物信息", "重新整理人物資訊" },
            { "ScanExternalSubtitleTask_Name_Scan_External_Subtitles", "Scan External Subtitles & Audio", "扫描外挂字幕与音轨", "掃描外掛字幕與音軌" },
            { "ScrapeIntroDbTask_Name_Scrape_Intro_Credits_from_IntroDb", "Scrape Intro & Credits Markers", "刮削片头片尾标记", "刮削片頭片尾標記" },
            { "MetadataEnhanceOptions_Group_AltMovieDb", "TMDB API", "TMDB 接口", "TMDB 介面" },
            { "MetadataEnhanceOptions_Group_CommunityRating", "Community Rating", "社区评分", "社群評分" },
            { "GeneralOptions_Group_ConfigTransfer", "Configuration Backup & Restore", "配置备份与恢复", "設定備份與還原" },
            { "DisclaimerDialog_EditorTitle_Disclaimer", "Disclaimer", "免责声明", "免責聲明" },
            { "MetadataEnhanceOptions_Group_DoubanAssist", "Douban", "豆瓣", "豆瓣" },
            { "ItemDisplayOptions_EditorTitle_Item_Display", "UI Display", "界面显示", "介面顯示" },
            { "LibraryOrderOptions_EditorTitle_Library_and_Order", "Libraries & Sorting", "媒体库与排序", "媒體庫與排序" },
            { "ExperienceEnhanceOptions_Group_Playback", "Playback, Cleanup & Safety", "播放、清理与安全", "播放、清理與安全" },
            { "MediaInfoExtractOptions_Group_ExtractionPerformance", "Control & Performance", "控制与性能", "控制與效能" },
            { "IntroSkipOptions_Group_Playback", "Playback Detection", "播放探测", "播放偵測" },
            { "NetworkProxyOptions_EditorTitle_Network_Direct_Link", "Network & API", "网络与接口", "網路與介面" },
            { "ScheduledTasks_EditorTitle_Scheduled_Tasks", "Common Tasks", "常用任务", "常用任務" },
            { "MetadataEnhanceOptions_Group_ScrapeEpisodeGroup", "Episode Scraping & Grouping", "剧集刮削与分组", "劇集刮削與分組" },
            { "MetadataEnhanceOptions_Group_EpisodeRefresh", "Episode Metadata Refresh", "单集元数据刷新", "單集元資料重新整理" },
            { "PluginOptions_EditorDescription_Basic_Settings", "Plugin status, tasks, and maintenance.", "插件启停、任务与维护。", "外掛啟停、任務與維護。" },
            { "MetadataEnhanceOptions_EditorDescription_Metadata_Enhance", "Scraping, ratings, people, episodes, and cache.", "刮削、评分、人物、剧集与缓存。", "刮削、評分、人物、劇集與快取。" },
            { "IntroSkipOptions_EditorDescription_Intro_Credits_Detection", "Playback detection, fingerprints, and online markers.", "播放探测、指纹与在线标记。", "播放偵測、指紋與線上標記。" },
            { "NetworkProxyOptions_EditorDescription_Network_Direct_Link", "Proxy, direct links, and TMDB API.", "代理、直链与 TMDB 接口。", "代理、直連與 TMDB 介面。" },
        };

        [Theory]
        [MemberData(nameof(CopyCases))]
        public void Copy_UsesApprovedTerms(string key, string english, string simplified, string traditional)
        {
            Assert.Equal(english, Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal(simplified, Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("zh-CN")));
            Assert.Equal(traditional, Resources.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("zh-Hant")));
        }
    }
}
