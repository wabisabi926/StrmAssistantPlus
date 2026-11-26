using System;
using System.Collections.Generic;
using System.IO;
using StrmAssistant.Mod;
using Xunit;

namespace StrmAssistant.Tests
{
    public class DeleteNotificationPolicyTests
    {
        [Fact]
        public void ShouldNotify_OnlyAfterSuccessfulRealDelete()
        {
            var state = new Dictionary<string, bool?> { ["movie.mkv"] = true };

            Assert.True(EnhanceNotificationSystem.ShouldNotifyDelete(null, state, false));
        }

        [Fact]
        public void ShouldNotNotify_WhenDryRun()
        {
            var state = new Dictionary<string, bool?> { ["movie.mkv"] = true };

            Assert.False(EnhanceNotificationSystem.ShouldNotifyDelete(null, state, true));
        }

        [Fact]
        public void ShouldNotNotify_WhenDeleteFailed()
        {
            var state = new Dictionary<string, bool?> { ["movie.mkv"] = true };

            Assert.False(EnhanceNotificationSystem.ShouldNotifyDelete(new IOException("failed"), state, false));
        }

        [Fact]
        public void ShouldNotNotify_WhenNoDeleteWasPrepared()
        {
            Assert.False(EnhanceNotificationSystem.ShouldNotifyDelete(null, null, false));
            Assert.False(EnhanceNotificationSystem.ShouldNotifyDelete(null,
                new Dictionary<string, bool?>(), false));
        }
    }
}
