using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace StrmAssistant.Core
{
    /// <summary>
    /// 一次目标解析的结果。找不到时保留可读的 assembly/type/method/signature 原因，
    /// 供 PatchTracker 与启动日志使用；不要把 Method 直接解引用。
    /// </summary>
    public sealed class TargetResolution
    {
        private TargetResolution(MethodInfo method, string description, string failureReason)
        {
            Method = method;
            Description = description;
            FailureReason = failureReason;
        }

        public MethodInfo Method { get; }

        public string Description { get; }

        public string FailureReason { get; }

        public bool IsFound => Method != null;

        public static TargetResolution Success(MethodInfo method, string description) =>
            new TargetResolution(method, description, null);

        public static TargetResolution Missing(string description, string reason) =>
            new TargetResolution(null, description, reason);

        public override string ToString() =>
            IsFound ? Description : $"{Description} ({FailureReason})";
    }

    /// <summary>
    /// 4.10+ 实现层被混淆后，禁止再按类型名硬找。这里集中提供三类稳定解析：
    /// 1) 从插件已注入的服务实例取具体实现类型；
    /// 2) 按接口枚举实现类型（用于未注入的服务）；
    /// 3) 按方法名/参数签名定位方法。
    /// </summary>
    public static class EmbyTargetResolver
    {
        private const BindingFlags AllMethods =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>
        /// 从运行中的服务实例取具体类型。4.11 上类型名被混淆，但实例本身仍由 Emby 注入。
        /// </summary>
        public static Type GetImplementationType(object serviceInstance) => serviceInstance?.GetType();

        /// <summary>
        /// 在指定程序集里找接口的唯一实现。找不到或多个实现时返回 null，由调用方记录结构化原因。
        /// </summary>
        public static Type FindImplementationType(Assembly assembly, Type interfaceType,
            Func<Type, bool> predicate = null)
        {
            if (assembly == null || interfaceType == null) return null;

            var matches = GetLoadableTypes(assembly)
                .Where(t => t != null && !t.IsInterface && !t.IsAbstract)
                .Where(t => ImplementsInterface(t, interfaceType))
                .Where(t => predicate == null || predicate(t))
                .ToList();

            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>
        /// 按方法名 + 精确参数签名找方法；parameterTypes 为 null 时只按名字找（要求唯一）。
        /// </summary>
        public static TargetResolution ResolveByName(Type type, string name, Type[] parameterTypes = null,
            Type returnType = null, bool? isStatic = null)
        {
            var description = Describe(type, name, parameterTypes, returnType, isStatic);
            if (type == null) return TargetResolution.Missing(description, "type not resolved");

            try
            {
                var matches = type.GetMethods(AllMethods)
                    .Where(m => m.Name == name)
                    .Where(m => isStatic == null || m.IsStatic == isStatic.Value)
                    .Where(m => returnType == null || m.ReturnType == returnType)
                    .Where(m => parameterTypes == null || ParametersMatch(m, parameterTypes))
                    .ToList();

                if (matches.Count == 1) return TargetResolution.Success(matches[0], description);
                if (matches.Count == 0)
                    return TargetResolution.Missing(description, "method not found");

                var preferred = matches
                    .OrderByDescending(m => m.IsPublic)
                    .ThenBy(m => m.DeclaringType == type ? 0 : 1)
                    .First();
                return TargetResolution.Success(preferred, description + $" [ambiguous {matches.Count}, picked {DescribeMethod(preferred)}]");
            }
            catch (Exception e)
            {
                return TargetResolution.Missing(description, e.Message);
            }
        }

        public static TargetResolution ResolvePropertySetter(Type type, string propertyName)
        {
            var description = $"{type?.FullName ?? "<unresolved>"}.{propertyName} setter";
            if (type == null) return TargetResolution.Missing(description, "type not resolved");

            try
            {
                var setter = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetSetMethod(true);
                return setter != null
                    ? TargetResolution.Success(setter, description)
                    : TargetResolution.Missing(description, "property setter not found");
            }
            catch (Exception e)
            {
                return TargetResolution.Missing(description, e.Message);
            }
        }

        /// <summary>
        /// 只按参数/返回类型找方法（4.11 混淆方法名时使用）。找不到唯一匹配就失败。
        /// </summary>
        public static TargetResolution ResolveBySignature(Type type, Type[] parameterTypes,
            Type returnType = null, bool? isStatic = null)
        {
            var description = Describe(type, null, parameterTypes, returnType, isStatic);
            if (type == null) return TargetResolution.Missing(description, "type not resolved");

            try
            {
                var matches = type.GetMethods(AllMethods)
                    .Where(m => isStatic == null || m.IsStatic == isStatic.Value)
                    .Where(m => returnType == null || m.ReturnType == returnType)
                    .Where(m => ParametersMatch(m, parameterTypes))
                    .ToList();

                if (matches.Count == 1) return TargetResolution.Success(matches[0], description);
                if (matches.Count == 0)
                    return TargetResolution.Missing(description, "method with signature not found");

                return TargetResolution.Missing(description,
                    $"ambiguous signature ({matches.Count}): {string.Join("; ", matches.Select(DescribeMethod))}");
            }
            catch (Exception e)
            {
                return TargetResolution.Missing(description, e.Message);
            }
        }

        /// <summary>
        /// 先按名字找（兼容 4.9/4.10），找不到再按签名找（兼容 4.11 混淆）。
        /// </summary>
        public static TargetResolution ResolveByNameOrSignature(Type type, string name, Type[] parameterTypes,
            Type returnType = null, bool? isStatic = null)
        {
            var byName = ResolveByName(type, name, parameterTypes, returnType, isStatic);
            if (byName.IsFound) return byName;

            var bySignature = ResolveBySignature(type, parameterTypes, returnType, isStatic);
            if (bySignature.IsFound) return bySignature;

            return TargetResolution.Missing(byName.Description,
                $"{byName.FailureReason}; signature fallback: {bySignature.FailureReason}");
        }

        public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null) return Array.Empty<Type>();

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static bool ImplementsInterface(Type candidate, Type interfaceType)
        {
            if (candidate == null || interfaceType == null) return false;
            if (interfaceType.IsGenericTypeDefinition)
            {
                return candidate.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
            }

            return interfaceType.IsAssignableFrom(candidate);
        }

        private static bool ParametersMatch(MethodInfo method, Type[] parameterTypes)
        {
            if (parameterTypes == null) return true;

            var parameters = method.GetParameters();
            if (parameters.Length != parameterTypes.Length) return false;

            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != parameterTypes[i]) return false;
            }

            return true;
        }

        private static string Describe(Type type, string name, Type[] parameterTypes, Type returnType,
            bool? isStatic)
        {
            var builder = new StringBuilder();
            builder.Append(type == null ? "<type not resolved>" : Sanitize(type.FullName));
            builder.Append('.');
            builder.Append(string.IsNullOrEmpty(name) ? "<signature>" : name);
            builder.Append('(');
            if (parameterTypes != null)
            {
                builder.Append(string.Join(", ", parameterTypes.Select(t => Sanitize(t.Name))));
            }
            else
            {
                builder.Append('*');
            }
            builder.Append(')');

            if (returnType != null) builder.Append(" -> ").Append(Sanitize(returnType.Name));
            if (isStatic.HasValue) builder.Append(isStatic.Value ? " [static]" : " [instance]");

            return builder.ToString();
        }

        private static string DescribeMethod(MethodInfo method)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => Sanitize(p.ParameterType.Name)));
            return $"{Sanitize(method.DeclaringType?.FullName)}.{method.Name}({parameters})";
        }

        /// <summary>混淆名含 Unicode 控制符，日志里替换成 '?' 避免破坏一行一条的格式。</summary>
        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsControl(chars[i])) chars[i] = '?';
            }

            return new string(chars);
        }
    }
}