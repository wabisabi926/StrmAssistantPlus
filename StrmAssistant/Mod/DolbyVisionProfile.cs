using HarmonyLib;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using StrmAssistant.Common;
using System;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 双层杜比 Vision Profile 7 标注（Wiki「提取 ISO 媒体信息」第 3 条）。
    ///
    /// 为什么挂 <c>ProbeResultNormalizer.GetMediaInfo</c>：这是 Emby 把 ffprobe 原始输出
    /// 归一化为 <see cref="MediaInfo"/>（继承 <c>MediaSourceInfo</c>）的出口，此时
    /// <c>MediaStreams</c> 已就绪，适合做一次只读的几何判定。
    ///
    /// 命中条件见 <see cref="DvProfileDetector"/>：HDR10 HEVC 基础层 + 半宽半高的 HEVC 增强层。
    /// 没有 DOVI 证据时仅接受恰好两条视频流的 MPEG-TS 几何配对；已有 DV 标签始终保留。
    /// 命中后把**增强层**写为 Dolby Vision Profile 7 —— 与 Emby 对双层 DV 的展示语义一致：
    /// 基础层保持 HDR10，增强层承载 DV 标记。
    ///
    /// 安全性：
    /// - 只写 <c>MediaStream.ExtendedVideoType / ExtendedVideoSubType</c>（Emby 定义为可写）；
    ///   <c>VideoRange</c>、<c>ExtendedVideoSubTypeDescription</c> 只读，不动。
    /// - **不改** <c>MediaSourceInfo.Path</c>，播放路径与条目身份不受影响。
    /// - 未命中（单层 / 非 hevc / 非 HDR10 基础层 / 无半尺寸增强层）一律不改。
    /// - 开关见 <see cref="Options.HdrTaggingOptions"/>（3.0.0.62 起与 ISO 提取解耦，默认 OFF）。
    /// </summary>
    public class DolbyVisionProfile : PatchBase<DolbyVisionProfile>
    {
        private static MethodInfo _getMediaInfo;

        public DolbyVisionProfile()
        {
            Initialize();

            // 与 ISO 提取解耦：改由「HDR 标注」分组控制（3.0.0.62 起）
            var hdr = Plugin.Instance.MediaInfoExtractStore.GetOptions().HdrTagging;

            if (hdr.EnableHdrTagging && hdr.EnableDolbyVisionProfileTagging)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var assembly = Assembly.Load("Emby.Server.MediaEncoding");
                var normalizer = assembly.GetType("Emby.Server.MediaEncoding.Probing.ProbeResultNormalizer");

                _getMediaInfo = normalizer?
                    .GetMethod("GetMediaInfo", BindingFlags.Instance | BindingFlags.Public);

                if (_getMediaInfo == null)
                {
                    Plugin.Instance.Logger.Warn(
                        "DolbyVisionProfile - ProbeResultNormalizer.GetMediaInfo not found; DV P7 tagging disabled");
                }
                else if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"DolbyVisionProfile - resolved {_getMediaInfo}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("DolbyVisionProfile init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            if (_getMediaInfo != null)
            {
                PatchUnpatch(PatchTracker, apply, _getMediaInfo, postfix: nameof(GetMediaInfoPostfix));
            }
        }

        [HarmonyPostfix]
        private static void GetMediaInfoPostfix(bool isAudio, MediaInfo __result)
        {
            try
            {
                string reason = null;
                if (isAudio || !TryTagDualLayer(__result, out reason))
                {
                    if (Plugin.Instance.DebugMode && !string.IsNullOrEmpty(reason))
                    {
                        Plugin.Instance.Logger.Debug($"DolbyVisionProfile - no dual-layer DV P7: {reason}");
                    }

                    return;
                }

                Plugin.Instance.Logger.Info($"DolbyVisionProfile - dual-layer DV P7 tagged on enhancement layer ({reason})");
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("DolbyVisionProfile postfix failed: " + ex.Message);
            }
        }

        internal static bool TryTagDualLayer(MediaInfo mediaInfo, out string reason)
        {
            reason = null;
            if (mediaInfo?.MediaStreams == null)
            {
                reason = "media info is unavailable";
                return false;
            }

            var videos = mediaInfo.MediaStreams
                .Where(stream => stream?.Type == MediaStreamType.Video)
                .ToList();
            if (videos.Count == 0)
            {
                reason = "no video streams";
                return false;
            }

            if (videos.Any(HasDoviEvidence))
            {
                reason = "existing Dolby Vision metadata found; leaving it unchanged";
                return false;
            }

            var container = mediaInfo.Container?.Trim().ToLowerInvariant();
            if (container != "mpegts" && container != "ts" && container != "m2ts")
            {
                reason = "geometry-only tagging requires an MPEG-TS source";
                return false;
            }

            if (videos.Count != 2)
            {
                reason = "geometry-only tagging requires exactly two video streams";
                return false;
            }

            if (!DvProfileDetector.TryDetectDualLayerProfile7(videos, out var enhancementLayer, out reason))
                return false;

            enhancementLayer.ExtendedVideoType = ExtendedVideoTypes.DolbyVision;
            enhancementLayer.ExtendedVideoSubType = ExtendedVideoSubTypes.DoviProfile76;
            return true;
        }

        private static bool HasDoviEvidence(MediaStream stream)
        {
            if (stream.ExtendedVideoType == ExtendedVideoTypes.DolbyVision) return true;
            if (stream.ExtendedVideoSubType.ToString().StartsWith("DoviProfile", StringComparison.Ordinal)) return true;
            return !string.IsNullOrEmpty(stream.VideoRange) &&
                   stream.VideoRange.IndexOf("dovi", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
