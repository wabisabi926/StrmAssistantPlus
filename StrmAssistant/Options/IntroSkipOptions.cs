using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Editors;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Core;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 片头片尾探测设置根节点。
    /// 设置项按功能拆到 3 个分组类：检测与指纹 / 在线刮削 / 播放与跳过。
    /// 本类只负责声明分组与委派 <c>Initialize()</c>。
    /// 注意：分组的属性名与分组类名不同（避免实例成员遮蔽类型名导致初始化表达式无法解析）。
    /// </summary>
    public class IntroSkipOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_IntroSkipOptions_Intro_Credits_Detection", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_IntroSkipOptions_Intro_Credits_Detection;

        /// <summary>声纹探测：解锁内置检测、指纹时长、并行计算、生效库范围与黑名单。</summary>
        [DisplayNameL("IntroSkipOptions_Group_Fingerprint", typeof(Resources))]
        public IntroSkipFingerprintOptions Fingerprint { get; set; } = new IntroSkipFingerprintOptions();

        /// <summary>行为探测：启用跳过、时长上限、生效范围、跳过偏好与清除标记。</summary>
        [DisplayNameL("IntroSkipOptions_Group_Playback", typeof(Resources))]
        public IntroSkipPlaybackOptions Playback { get; set; } = new IntroSkipPlaybackOptions();

        /// <summary>在线刮削：从 TheIntroDB / TIDB 拉取标记（只补缺失）。</summary>
        [DisplayNameL("IntroSkipOptions_Group_Scrape", typeof(Resources))]
        public IntroSkipScrapeOptions Scrape { get; set; } = new IntroSkipScrapeOptions();

        /// <summary>Store 在保存时用于判断 patch 是否可用（与各组内的同名 helper 独立）。</summary>
        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public override IEditObjectContainer CreateEditContainer()
        {
            LocalizationHelper.PrepareForView();
            var container = base.CreateEditContainer();
            if (container is EditObjectContainer editContainer)
            {
                var isEmby411 = EmbyVersionAdapter.GetCurrentApiVersion() >= EmbyApiVersion.V4_11;
                EditorGroupHelper.Rebuild(editContainer,
                    new EditorGroupSpec(Resources.IntroSkipOptions_Group_Playback,
                        "Playback", "Playback"),
                    new EditorGroupSpec(isEmby411
                        ? Resources.IntroSkipOptions_Group_IntroCreditsDetection
                        : Resources.IntroSkipOptions_Group_Fingerprint,
                        "Fingerprint", "Fingerprint"),
                    new EditorGroupSpec(Resources.IntroSkipOptions_Group_Scrape,
                        "Scrape", "Scrape"));

                LocalizationHelper.ApplyDescriptions(editContainer, this);

                if (isEmby411)
                {
                    var fingerprintGroup = editContainer.EditorRoot.EditorItems.OfType<EditorGroup>()
                        .FirstOrDefault(group => group.Name == "Fingerprint");
                    if (fingerprintGroup?.EditorItems != null)
                    {
                        var unlockIntroSkip = fingerprintGroup.EditorItems
                            .FirstOrDefault(item => item.Name == nameof(IntroSkipFingerprintOptions.UnlockIntroSkip));
                        if (unlockIntroSkip != null)
                        {
                            unlockIntroSkip.DisplayName = Resources.IntroSkipOptions_UnlockIntroSkip_Intro_Credits_Enhanced;
                            unlockIntroSkip.Description = Resources.IntroSkipOptions_UnlockIntroSkip_Intro_Credits_Description;
                        }
                    }
                }
            }

            return container;
        }

        public void Initialize(ILibraryManager libraryManager)
        {
            Fingerprint.Initialize(libraryManager);
            Playback.Initialize(libraryManager);
            Scrape.Initialize(libraryManager);
        }
    }
}
