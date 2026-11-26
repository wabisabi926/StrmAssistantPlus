using MediaBrowser.Controller.Providers;
using StrmAssistant.Core;
using System;
using System.Reflection;

namespace StrmAssistant.Common
{
    /// <summary>
    /// MovieDb 2.0.1.0（Emby 4.11）把旧 GetMetadata(info, ct) 改成了
    /// throw NotImplementedException，真正入口是 RemoteMetadataFetchOptions&lt;T&gt; 重载。
    /// 4.9/4.10 的 MovieDb 1.x 仍只有旧重载，所以这里优先 options、回退旧签名。
    /// </summary>
    public static class MovieDbTargets
    {
        public static TargetResolution ResolveGetMetadataTarget(Assembly movieDbAssembly, string providerTypeName,
            Type lookupInfoType)
        {
            var description = $"{providerTypeName}.GetMetadata({lookupInfoType?.Name ?? "<null>"})";

            if (movieDbAssembly == null || lookupInfoType == null)
                return TargetResolution.Missing(description, "MovieDb assembly or lookup info type missing");

            var providerType = movieDbAssembly.GetType(providerTypeName);
            if (providerType == null)
                return TargetResolution.Missing(description, $"type '{providerTypeName}' not found");

            var fetchOptionsType = typeof(RemoteMetadataFetchOptions<>).MakeGenericType(lookupInfoType);
            var optionsOverload = EmbyTargetResolver.ResolveByName(providerType, "GetMetadata",
                new[] { fetchOptionsType, typeof(System.Threading.CancellationToken) });

            if (optionsOverload.IsFound)
                return TargetResolution.Success(optionsOverload.Method, optionsOverload.Description);

            var legacyOverload = EmbyTargetResolver.ResolveByName(providerType, "GetMetadata",
                new[] { lookupInfoType, typeof(System.Threading.CancellationToken) });

            return legacyOverload.IsFound
                ? TargetResolution.Success(legacyOverload.Method, legacyOverload.Description)
                : TargetResolution.Missing(description,
                    "neither RemoteMetadataFetchOptions nor legacy overload found");
        }

        public static MethodInfo ResolveGetMetadata(Assembly movieDbAssembly, string providerTypeName,
            Type lookupInfoType)
        {
            return ResolveGetMetadataTarget(movieDbAssembly, providerTypeName, lookupInfoType).Method;
        }

        public static bool IsFetchOptionsOverload(MethodInfo method, Type lookupInfoType)
        {
            if (method == null || lookupInfoType == null) return false;

            var parameters = method.GetParameters();
            if (parameters.Length == 0) return false;

            var fetchOptionsType = typeof(RemoteMetadataFetchOptions<>).MakeGenericType(lookupInfoType);
            return parameters[0].ParameterType == fetchOptionsType;
        }
    }
}