using HarmonyLib;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Core;
using System;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Text;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 自定义本地发现地址（<c>NetworkOptions.LocalDiscoveryUrl</c>）。
    ///
    /// 局域网客户端通过 Emby 的 UDP 发现协议（「who is EmbyServer?」）获取服务器地址；
    /// 当部署在反向代理之后时，希望发现响应返回代理地址而不是内网直连地址。
    ///
    /// 挂钩点：<c>Emby.Server.Implementations.Udp.UdpServer.SendMessage(string localUrl, IPEndPoint, Encoding)</c>
    /// —— 前缀改写 <c>localUrl</c>。**只影响 UDP 发现响应体**，不改动全局 <c>GetLocalApiUrl</c>，
    /// 因此不会波及网页/API/播放等其它本地地址。
    ///
    /// 地址为空时不打补丁，零行为变化；方法缺失静默跳过。
    /// </summary>
    public class CustomLocalDiscovery : PatchBase<CustomLocalDiscovery>
    {
        private static MethodInfo _sendMessage;
        private static MethodInfo _serializeToString;

        public CustomLocalDiscovery()
        {
            PatchTracker.IsFeatureEnabled = !string.IsNullOrWhiteSpace(GetOverrideUrl());

            Initialize();

            if (PatchTracker.IsFeatureEnabled) Patch();
        }

        private static string GetOverrideUrl() =>
            Plugin.Instance.NetworkProxyStore.GetOptions().Network.LocalDiscoveryUrl;

        protected override void OnInitialize()
        {
            try
            {
                var assembly = Assembly.Load("Emby.Server.Implementations");

                if (EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11)
                {
                    var serializerType = EmbyTargetResolver.FindImplementationType(assembly,
                        typeof(IJsonSerializer), type => type.GetMethods(
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Any(m => m.Name == "SerializeToString" &&
                                      m.GetParameters().Length == 1 &&
                                      m.GetParameters()[0].ParameterType == typeof(object)));
                    _serializeToString = Require(EmbyTargetResolver.ResolveByName(serializerType,
                        "SerializeToString", new[] { typeof(object) }, typeof(string), false)).Method;

                    if (_serializeToString == null)
                    {
                        Plugin.Instance.Logger.Warn(
                            "CustomLocalDiscovery - discovery response serializer not found; custom discovery url disabled");
                    }

                    return;
                }

                var expected = new[] { typeof(string), typeof(IPEndPoint), typeof(Encoding) };

                bool HasTarget(Type type) => type != null && type.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length == 3 && ps[0].ParameterType == expected[0] &&
                               ps[1].ParameterType == expected[1] && ps[2].ParameterType == expected[2];
                    });

                // 4.11 的 UdpServer 类型名被混淆；从 UdpServerEntryPoint 的字段/属性类型反查。
                var udpEntryPoint = assembly.GetType("Emby.Server.Implementations.Udp.UdpServerEntryPoint");
                var udpServerType = udpEntryPoint?
                    .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(f => f.FieldType)
                    .FirstOrDefault(HasTarget);

                if (udpServerType == null)
                {
                    udpServerType = EmbyTargetResolver.GetLoadableTypes(assembly).FirstOrDefault(HasTarget);
                }

                _sendMessage = Require(EmbyTargetResolver.ResolveBySignature(udpServerType,
                    expected, typeof(void), false)).Method;

                if (_sendMessage == null)
                {
                    Plugin.Instance.Logger.Warn(
                        "CustomLocalDiscovery - UdpServer.SendMessage not found; custom discovery url disabled");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("CustomLocalDiscovery init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            if (EmbyVersionAdapter.Instance.ApiVersion >= EmbyApiVersion.V4_11)
            {
                if (_serializeToString != null)
                {
                    PatchUnpatch(PatchTracker, apply, _serializeToString,
                        prefix: nameof(SerializeToStringPrefix));
                }

                return;
            }

            if (_sendMessage != null)
            {
                PatchUnpatch(PatchTracker, apply, _sendMessage, prefix: nameof(SendMessagePrefix));
            }
        }

        [HarmonyPrefix]
        private static void SerializeToStringPrefix(object __0)
        {
            if (__0 is ServerDiscoveryInfo discovery)
            {
                discovery.Address = ApplyOverrideUrl(discovery.Address);
            }
        }

        [HarmonyPrefix]
        private static void SendMessagePrefix(ref string __0)
        {
            __0 = ApplyOverrideUrl(__0);
        }

        private static string ApplyOverrideUrl(string url)
        {
            try
            {
                var custom = GetOverrideUrl();
                if (string.IsNullOrWhiteSpace(custom)) return url;

                custom = custom.Trim().TrimEnd('/');
                if (string.Equals(custom, url, StringComparison.OrdinalIgnoreCase)) return url;

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"CustomLocalDiscovery - replaced discovery url: {custom}");
                }

                return custom;
            }
            catch
            {
                // 忽略，保持原地址
                return url;
            }
        }
    }
}
