using MediaBrowser.Model.Dto;
using StrmAssistant.Common;
using StrmAssistant.Mod;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    /// 直链日志脱敏（回归 SA-05）。
    ///
    /// 网盘的访问签名、pickcode、临时凭据几乎都在 query 里。旧实现把完整地址
    /// （还做过 URL 解码，等于让签名更易读）写进 Info 日志，日志一旦外发就等于交出可用链接。
    /// </summary>
    public class StrmDirectRedirectLogTests
    {
        [Fact]
        public void Removes_Query_UserInfo_And_Fragment()
        {
            const string url =
                "http://user:pass@192.168.50.250:12333/115/url/video.mp4?pickcode=abc123&userid=341871580#frag";

            var safe = UrlSanitizer.ToLogSafe(url);

            Assert.Equal("http://192.168.50.250:12333/115/url/video.mp4", safe);
            Assert.DoesNotContain("pickcode", safe);
            Assert.DoesNotContain("abc123", safe);
            Assert.DoesNotContain("user:pass", safe);
            Assert.DoesNotContain("341871580", safe);
        }

        [Fact]
        public void Keeps_DefaultPort_Out_Of_Result()
        {
            Assert.Equal("https://example.com/video.mp4",
                UrlSanitizer.ToLogSafe("https://example.com/video.mp4?sig=xyz"));
        }

        [Fact]
        public void NormalizeRedirectUrl_PreservesSignedQueryBytes()
        {
            const string url = "https://example.com/video.mp4?a=1+2&b=%2B&a=3&empty=#frag";

            Assert.Equal(url, StrmDirectRedirect.NormalizeRedirectUrl(url));
        }

        [Fact]
        public void LiveTvDirectLink_RejectsUnsafeSources()
        {
            var direct = new MediaSourceInfo
            {
                Path = "https://example.com/live.m3u8",
                SupportsDirectPlay = true
            };
            Assert.True(LiveTvDirectRedirect.CanMarkDirectPlayable(direct));

            direct.RequiredHttpHeaders = new Dictionary<string, string> { ["Authorization"] = "token" };
            Assert.False(LiveTvDirectRedirect.CanMarkDirectPlayable(direct));

            direct.RequiredHttpHeaders = null;
            direct.Path = "file:///D:/media/live.m3u8";
            Assert.False(LiveTvDirectRedirect.CanMarkDirectPlayable(direct));

            direct.Path = "https://example.com/live.m3u8";
            direct.SupportsDirectPlay = false;
            direct.SupportsDirectStream = false;
            Assert.False(LiveTvDirectRedirect.CanMarkDirectPlayable(direct));
        }

        [Fact]
        public void VideoRedirect_RejectsRequiredHeadersBeforeBranching()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var source = File.ReadAllText(Path.Combine(root, "StrmAssistant/Mod/StrmDirectRedirect.cs"));
            var methodStart = source.IndexOf("private static bool CanRedirect", StringComparison.Ordinal);
            var methodEnd = source.IndexOf("private static void DisposeState", methodStart, StringComparison.Ordinal);
            var method = source.Substring(methodStart, methodEnd - methodStart);
            var guard = method.IndexOf("mediaSource.RequiredHttpHeaders != null", StringComparison.Ordinal);
            var branch = method.IndexOf("if (isVideo)", StringComparison.Ordinal);

            Assert.True(guard >= 0);
            Assert.True(branch > guard);
        }

        [Fact]
        public void Handles_Empty_And_Unparsable()
        {
            Assert.Equal("(empty)", UrlSanitizer.ToLogSafe(null));
            Assert.Equal("(empty)", UrlSanitizer.ToLogSafe("  "));
            Assert.Equal("(unparsable)", UrlSanitizer.ToLogSafe("not a url"));
        }
    }
}
