using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 后台任务统一出口。
    ///
    /// 之前大量 <c>_ = Task.Run(...)</c> 直接丢弃任务引用：内部抛异常时没人观察，
    /// 只会变成「功能没生效但日志干净」——外部审查提到的「未追踪的后台任务」即此。
    /// 这里统一做三件事：命名、捕获异常、登记运行中数量，便于排查。
    ///
    /// 注意：本类只负责「可丢弃」的后台任务；需要结果或需要 await 的场景仍应直接 await。
    /// </summary>
    public static class BackgroundTaskRunner
    {
        private static readonly ConcurrentDictionary<long, string> Running =
            new ConcurrentDictionary<long, string>();
        private static readonly ConcurrentDictionary<long, Task> Tasks =
            new ConcurrentDictionary<long, Task>();
        private static readonly object LifecycleLock = new object();
        private static int _acceptingTasks = 1;

        private static long _sequence;

        /// <summary>当前运行中的后台任务数，用于运行状态页与诊断。</summary>
        public static int RunningCount => Running.Count;

        public static Task Run(string name, Func<Task> body)
        {
            if (body == null) return Task.CompletedTask;

            var id = Interlocked.Increment(ref _sequence);
            Task task;
            lock (LifecycleLock)
            {
                if (_acceptingTasks == 0) return Task.CompletedTask;
                Running[id] = name ?? "unnamed";
                task = Task.Run(async () =>
                {
                    try
                    {
                        await body().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 主动取消属正常路径，不记为错误
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            Plugin.Instance?.Logger?.Error($"BackgroundTask - {name} failed: {e.Message}");
                            Plugin.Instance?.Logger?.Debug(e.StackTrace);
                        }
                        catch
                        {
                            // 日志本身失败时不能再抛，否则会终结进程
                        }
                    }
                    finally
                    {
                        Running.TryRemove(id, out _);
                        Tasks.TryRemove(id, out _);
                    }
                });
                Tasks[id] = task;
            }

            return task;
        }

        public static void Reset()
        {
            lock (LifecycleLock)
            {
                _acceptingTasks = 1;
                Running.Clear();
                Tasks.Clear();
            }
        }

        public static void BeginShutdown()
        {
            lock (LifecycleLock)
            {
                _acceptingTasks = 0;
            }
        }

        public static async Task<bool> ShutdownAsync(TimeSpan timeout)
        {
            BeginShutdown();
            Task[] pending;
            lock (LifecycleLock)
            {
                pending = Tasks.Values.ToArray();
            }

            if (pending.Length == 0) return true;
            var all = Task.WhenAll(pending);
            var completed = await Task.WhenAny(all, Task.Delay(timeout)).ConfigureAwait(false);
            return completed == all;
        }

        public static Task Run(string name, Action body)
        {
            if (body == null) return Task.CompletedTask;

            return Run(name, () =>
            {
                body();
                return Task.CompletedTask;
            });
        }
    }
}
