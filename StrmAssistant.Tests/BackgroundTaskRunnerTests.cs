using System;
using System.Threading.Tasks;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    /// 后台任务统一出口：异常必须被吞掉并记录，任务计数必须归零。
    /// 之前 `_ = Task.Run(...)` 丢弃任务引用，内部异常没人观察，表现为「功能没生效但日志干净」。
    /// </summary>
    public class BackgroundTaskRunnerTests
    {
        [Fact]
        public async Task RunsBody()
        {
            var ran = false;

            await BackgroundTaskRunner.Run("test.runs", () => { ran = true; });

            Assert.True(ran);
        }

        [Fact]
        public async Task SwallowsExceptions_AndRestoresCounter()
        {
            await BackgroundTaskRunner.Run("test.throws", () => throw new InvalidOperationException("boom"));

            // 不抛到调用方，且计数归零（等待后应稳定为 0）
            await Task.Delay(50);
            Assert.Equal(0, BackgroundTaskRunner.RunningCount);
        }

        [Fact]
        public async Task HandlesNullBody()
        {
            await BackgroundTaskRunner.Run("test.null", (Action)null);
            await BackgroundTaskRunner.Run("test.null2", (Func<Task>)null);

            Assert.Equal(0, BackgroundTaskRunner.RunningCount);
        }

        [Fact]
        public async Task Shutdown_StopsNewTasksAndWaitsForRunningTasks()
        {
            BackgroundTaskRunner.Reset();
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var running = BackgroundTaskRunner.Run("test.shutdown.running", () => release.Task);
            var ranAfterShutdown = false;

            var early = await BackgroundTaskRunner.ShutdownAsync(TimeSpan.FromMilliseconds(50));
            Assert.False(early);

            await BackgroundTaskRunner.Run("test.shutdown.rejected", () => { ranAfterShutdown = true; });
            Assert.False(ranAfterShutdown);

            release.SetResult(true);
            var drained = await BackgroundTaskRunner.ShutdownAsync(TimeSpan.FromSeconds(1));
            await running;
            Assert.True(drained);
            BackgroundTaskRunner.Reset();
        }

        [Fact]
        public async Task RunsAsyncBody()
        {
            var value = 0;

            await BackgroundTaskRunner.Run("test.async", async () =>
            {
                await Task.Yield();
                value = 42;
            });

            Assert.Equal(42, value);
        }
    }
}
