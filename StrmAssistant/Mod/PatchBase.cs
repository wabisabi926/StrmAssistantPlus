using System;
using System.Collections.Generic;
using System.Linq;
using StrmAssistant.Core;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public abstract class PatchBase<T> where T : PatchBase<T>
    {
        public PatchTracker PatchTracker;

        private readonly List<TargetResolution> _requiredTargets = new List<TargetResolution>();

        public static T Instance { get; private set; }

        protected PatchBase()
        {
            Instance = (T)this;
            PatchTracker = new PatchTracker(typeof(T), PatchApproach.Harmony);
        }

        /// <summary>
        /// 登记一个必需目标。OnInitialize 结束后统一校验；缺失时模块进入 NotSupported，
        /// 不再出现「GetType 返回 null → 空引用」的崩溃式失败。
        /// </summary>
        protected TargetResolution Require(TargetResolution resolution)
        {
            if (resolution != null) _requiredTargets.Add(resolution);
            return resolution;
        }

        protected void Initialize()
        {
            PatchTracker.Status = PatchStatus.Initializing;

            try
            {
                OnInitialize();
                ApplyTargetRequirements();

                if (PatchTracker.Status == PatchStatus.NotSupported)
                {
                    PatchTracker.FallbackPatchApproach = PatchApproach.None;
                    return;
                }

                PatchTracker.Status = PatchStatus.Initialized;
                PatchTracker.InitializedAt = DateTime.Now;

                // ★ 统一 debug 日志：模块 OnInitialize 成功
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"[{PatchTracker.PatchType.Name}] ✓ OnInitialize OK - Status={PatchTracker.Status}, Fallback={PatchTracker.FallbackPatchApproach}");
                }
                else
                {
                    Plugin.Instance.Logger.Info($"[{PatchTracker.PatchType.Name}] OnInitialize completed - Status={PatchTracker.Status}");
                }
            }
            catch (Exception e)
            {
                PatchTracker.MarkInitializationFailed($"Initialization failed: {e.Message}");

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"[{PatchTracker.PatchType.Name}] ✗ OnInitialize EXCEPTION: {e.Message}");
                    Plugin.Instance.Logger.Debug(e.StackTrace);
                }

                Plugin.Instance.Logger.Warn($"{PatchTracker.PatchType.Name} Init Failed: {e.Message}");
                return;
            }

            if (PatchTracker.FallbackPatchApproach == PatchApproach.None)
            {
                PatchTracker.Status = PatchStatus.NotSupported;
                return;
            }

            if (HarmonyMod is null)
            {
                PatchTracker.FallbackPatchApproach = PatchApproach.Reflection;
                Plugin.Instance.Logger.Debug($"{PatchTracker.PatchType.Name} using Reflection (Harmony unavailable)");
            }
        }

        private void ApplyTargetRequirements()
        {
            var missing = _requiredTargets.Where(t => t != null && !t.IsFound).ToList();
            if (missing.Count == 0) return;

            foreach (var target in missing)
            {
                PatchTracker.MissingTargets.Add(target.Description);
                PatchTracker.AddError($"target not found: {target}");
                Plugin.Instance.Logger.Warn(
                    $"[{PatchTracker.PatchType.Name}] target not found: {target.Description} ({target.FailureReason})");
            }

            PatchTracker.FallbackPatchApproach = PatchApproach.None;
            PatchTracker.Status = PatchStatus.NotSupported;
        }

        protected abstract void OnInitialize();

        protected abstract void Prepare(bool apply);

        public void Patch()
        {
            if (PatchTracker.Status == PatchStatus.Failed) return;

            if (PatchTracker.FallbackPatchApproach == PatchApproach.None || PatchTracker.MissingTargets.Count > 0)
            {
                PatchTracker.Status = PatchStatus.NotSupported;

                if (Plugin.Instance?.DebugMode == true)
                {
                    Plugin.Instance.Logger.Debug(
                        $"[{PatchTracker.PatchType.Name}] Patch() skipped - unsupported on this Emby build");
                }

                return;
            }

            if (Plugin.Instance?.DebugMode == true)
            {
                Plugin.Instance.Logger.Debug($"[{PatchTracker.PatchType.Name}] ▶ Patch() applying (Fallback={PatchTracker.FallbackPatchApproach})");
            }

            var hadErrors = PatchTracker.HasErrors;

            try
            {
                Prepare(true);
            }
            catch (Exception e)
            {
                PatchTracker.MarkFailed($"Patch application failed: {e.Message}");
                Plugin.Instance?.Logger?.Warn($"{PatchTracker.PatchType.Name} Patch Failed: {e.Message}");
                return;
            }

            PatchTracker.CompletePatch();

            if (!hadErrors && PatchTracker.HasErrors)
            {
                Plugin.Instance?.Logger?.Warn(
                    $"[{PatchTracker.PatchType.Name}] 部分补丁未能应用，功能可能不完整（详见运行时状态页）");
            }

            if (Plugin.Instance?.DebugMode == true)
            {
                Plugin.Instance.Logger.Debug($"[{PatchTracker.PatchType.Name}] Patch() finished - Status={PatchTracker.Status}, Healthy={PatchTracker.IsHealthy}");
            }
        }

        public void Unpatch()
        {
            if (Plugin.Instance.DebugMode)
            {
                Plugin.Instance.Logger.Debug($"[{PatchTracker.PatchType.Name}] ▶ Unpatch() called");
            }

            Prepare(false);
            PatchTracker.Status = PatchStatus.NotInitialized;
        }
    }
}
