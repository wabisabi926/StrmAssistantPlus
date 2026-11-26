using StrmAssistant.Options;
using System;
using System.Globalization;
using Xunit;

namespace StrmAssistant.Tests
{
    public class BrandOptionsTests
    {
        [Theory]
        [InlineData("zh-CN", "插件版本：v{0} 💎 Emby 适配基线：4.11.0.19")]
        [InlineData("zh-Hant", "外掛版本：v{0} 💎 Emby 適配基準：4.11.0.19")]
        [InlineData("en-US", "Plugin: v{0} 💎 Emby compatibility baseline: 4.11.0.19")]
        public void BrandLine_UsesLocalizedProfessionalFormat(string cultureName, string template)
        {
            var original = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
                var text = new BrandOptions().PluginVersion.Text;

                Assert.Equal(string.Format(template, AboutOptions.GetVersion()), text);
                Assert.Contains("💎", text);
                Assert.DoesNotContain("｜", text);
                Assert.DoesNotContain(" | ", text);
                Assert.DoesNotContain("+", text);
            }
            finally
            {
                CultureInfo.CurrentUICulture = original;
            }
        }

        [Fact]
        public void DisplayVersion_KeepsFourSegments()
        {
            // 4.0.1.0 曾因旧版「砍结尾 .0」逻辑被显示成 4.0.1，
            // 与 tag/CHANGELOG/Release 名称不一致（见 issue 截图）。
            var version = AboutOptions.GetVersion();

            Assert.Equal(4, version.Split('.').Length);
            Assert.DoesNotContain(" ", version);
        }

        [Fact]
        public void BrandLine_VersionMatchesAssemblyFourSegments()
        {
            var text = new BrandOptions().PluginVersion.Text;
            var version = AboutOptions.GetVersion();

            Assert.Contains("v" + version, text);
        }
    }
}
