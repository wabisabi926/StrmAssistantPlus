using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.PropertyDiff;
using MediaBrowser.Common;
using MediaBrowser.Model.Logging;
using StrmAssistant.Mod;
using StrmAssistant.Options.UIBaseClasses.Store;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using static StrmAssistant.Common.CommonUtility;

namespace StrmAssistant.Options.Store
{
    /// <summary>
    /// 网络与直链设置存储：代理、影音直链、TMDB 替换三块的 URL 规整、可达性探测与补丁即时生效。
    /// 原先这些逻辑分散在 <see cref="PluginOptionsStore"/> 与 <see cref="MetadataEnhanceOptionsStore"/>。
    /// </summary>
    public class NetworkProxyOptionsStore : SimpleFileStore<NetworkProxyOptions>
    {
        private readonly ILogger _logger;
        private readonly HashSet<string> _pendingChangedProperties = new HashSet<string>();

        public NetworkProxyOptionsStore(IApplicationHost applicationHost, ILogger logger, string pluginFullName)
            : base(applicationHost, logger, pluginFullName)
        {
            _logger = logger;

            FileSaving += OnFileSaving;
            FileSaved += OnFileSaved;
        }

        public NetworkProxyOptions NetworkProxyOptions => GetOptions();

        private static string NormalizeUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url) ? url.Trim().TrimEnd('/') : url?.Trim();
        }

        private void OnFileSaving(object sender, FileSavingEventArgs e)
        {
            if (!(e.Options is NetworkProxyOptions options)) return;
            _pendingChangedProperties.Clear();
            if (!RuntimeActive) return;

            var network = options.Network;
            var tmdb = options.TmdbReplacement;

            network.ProxyServerUrl = NormalizeUrl(network.ProxyServerUrl);
            network.LocalDiscoveryUrl = NormalizeUrl(network.LocalDiscoveryUrl);
            tmdb.AltMovieDbApiUrl = NormalizeUrl(tmdb.AltMovieDbApiUrl);
            tmdb.AltMovieDbImageUrl = NormalizeUrl(tmdb.AltMovieDbImageUrl);

            // 代理可达性探测（只影响状态展示，不阻断保存）
            if (network.EnableProxyServer && !string.IsNullOrWhiteSpace(network.ProxyServerUrl))
            {
                if (TryParseProxyUrl(network.ProxyServerUrl, out var schema, out var host, out var port,
                        out var username, out var password) &&
                    CheckProxyReachability(schema, host, port, username, password) is (true, var httpPing))
                {
                    network.ProxyServerStatus.Status = ItemStatus.Succeeded;
                    network.ProxyServerStatus.Caption = Resources.ProxyServer_Available;
                    network.ProxyServerStatus.StatusText = $"{httpPing} ms";
                }
                else
                {
                    network.ProxyServerStatus.Status = ItemStatus.Unavailable;
                    network.ProxyServerStatus.Caption = Resources.ProxyServer_Unavailable;
                    network.ProxyServerStatus.StatusText = "N/A";
                }

                network.ShowProxyServerStatus = true;
            }
            else
            {
                network.ProxyServerStatus.StatusText = string.Empty;
                network.ShowProxyServerStatus = false;
            }

            var changes = PropertyChangeDetector.DetectObjectPropertyChanges(NetworkProxyOptions, options);
            _pendingChangedProperties.UnionWith(changes.Select(c => c.PropertyName));

            // 直链字段位于嵌套对象中；在写盘前显式记录比较结果，供 FileSaved 应用。
            var oldDirectLink = NetworkProxyOptions.DirectLink ?? new DirectLinkOptions();
            var newDirectLink = options.DirectLink ?? new DirectLinkOptions();
            if (oldDirectLink.EnableStrmDirectRedirect != newDirectLink.EnableStrmDirectRedirect)
                _pendingChangedProperties.Add(nameof(DirectLinkOptions.EnableStrmDirectRedirect));
            if (oldDirectLink.StrmDirectRedirectFollow302 != newDirectLink.StrmDirectRedirectFollow302)
                _pendingChangedProperties.Add(nameof(DirectLinkOptions.StrmDirectRedirectFollow302));
            if (!string.Equals(oldDirectLink.StrmVideoDirectRedirectClientBlacklist,
                    newDirectLink.StrmVideoDirectRedirectClientBlacklist))
                _pendingChangedProperties.Add(nameof(DirectLinkOptions.StrmVideoDirectRedirectClientBlacklist));
            if (!string.Equals(oldDirectLink.StrmAudioDirectRedirectClientBlacklist,
                    newDirectLink.StrmAudioDirectRedirectClientBlacklist))
                _pendingChangedProperties.Add(nameof(DirectLinkOptions.StrmAudioDirectRedirectClientBlacklist));
            if (oldDirectLink.EnableLiveTvDirectRedirect != newDirectLink.EnableLiveTvDirectRedirect)
                _pendingChangedProperties.Add(nameof(DirectLinkOptions.EnableLiveTvDirectRedirect));
        }

        private void OnFileSaved(object sender, FileSavedEventArgs e)
        {
            if (!(e.Options is NetworkProxyOptions options)) return;
            var changedProperties = new HashSet<string>(_pendingChangedProperties);
            _pendingChangedProperties.Clear();
            var network = options.Network;
            var tmdb = options.TmdbReplacement;

            // 代理服务器
            if (changedProperties.Contains(nameof(NetworkOptions.EnableProxyServer)))
            {
                if (network.EnableProxyServer)
                {
                    PatchManager.EnableProxyServer.Patch();
                }
                else
                {
                    PatchManager.EnableProxyServer.Unpatch();
                }
            }

            if (changedProperties.Contains(nameof(NetworkOptions.ProxyServerUrl)) ||
                changedProperties.Contains(nameof(NetworkOptions.EnableProxyServer)))
            {
                if (network.EnableProxyServer && network.ProxyServerStatus.Status == ItemStatus.Succeeded)
                {
                    Plugin.Instance.ApplicationHost.NotifyPendingRestart();
                }
                else if (!network.EnableProxyServer)
                {
                    Plugin.Instance.ApplicationHost.NotifyPendingRestart();
                }
            }

            // 本地发现地址
            if (changedProperties.Contains(nameof(NetworkOptions.LocalDiscoveryUrl)))
            {
                if (!string.IsNullOrWhiteSpace(network.LocalDiscoveryUrl))
                {
                    PatchManager.CustomLocalDiscovery.Patch();
                }
                else
                {
                    PatchManager.CustomLocalDiscovery.Unpatch();
                }
            }

            // 影音直链：使用 FileSaving 记录的变更，避免落盘后新旧对象已相同。
            try
            {
                var newDirectLink = options.DirectLink ?? new DirectLinkOptions();

                if (changedProperties.Contains(nameof(DirectLinkOptions.EnableStrmDirectRedirect)) ||
                    changedProperties.Contains(nameof(DirectLinkOptions.StrmDirectRedirectFollow302)) ||
                    changedProperties.Contains(nameof(DirectLinkOptions.StrmVideoDirectRedirectClientBlacklist)) ||
                    changedProperties.Contains(nameof(DirectLinkOptions.StrmAudioDirectRedirectClientBlacklist)))
                {
                    PatchManager.StrmDirectRedirect?.ApplyOptionsAndConfigure(newDirectLink);
                }

                if (changedProperties.Contains(nameof(DirectLinkOptions.EnableLiveTvDirectRedirect)))
                {
                    PatchManager.LiveTvDirectRedirect?.ApplyOptionsAndConfigure(newDirectLink);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"影音直链选项即时生效失败: {ex.Message}");
            }

            // TMDB 替换
            if (changedProperties.Contains(nameof(AltMovieDbOptions.AltMovieDbConfig)))
            {
                if (tmdb.AltMovieDbConfig)
                {
                    PatchManager.AltMovieDbConfig.PatchApiUrl();
                    if (!string.IsNullOrEmpty(tmdb.AltMovieDbImageUrl))
                    {
                        PatchManager.AltMovieDbConfig.PatchImageUrl();
                    }
                }
                else
                {
                    PatchManager.AltMovieDbConfig.UnpatchApiUrl();
                    if (!string.IsNullOrEmpty(tmdb.AltMovieDbImageUrl))
                    {
                        PatchManager.AltMovieDbConfig.UnpatchImageUrl();
                    }
                }
            }

            if (changedProperties.Contains(nameof(AltMovieDbOptions.AltMovieDbImageUrl)))
            {
                if (!string.IsNullOrEmpty(tmdb.AltMovieDbImageUrl))
                {
                    PatchManager.AltMovieDbConfig.PatchImageUrl();
                }
                else
                {
                    PatchManager.AltMovieDbConfig.UnpatchImageUrl();
                }
            }
        }
    }
}
