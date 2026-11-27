using StrmAssistant.Common;
using StrmAssistant.Core;
using Xunit;

namespace StrmAssistant.Tests
{
    public class DtoPatchPolicyTests
    {
        [Theory]
        [InlineData(EmbyApiVersion.V4_9_1)]
        [InlineData(EmbyApiVersion.V4_10)]
        public void OlderVersions_UseBatchDtoPatch(EmbyApiVersion version)
        {
            Assert.True(DtoPatchPolicy.UseBatchDto(version));
        }

        [Fact]
        public void Emby411_UsesModelSettersInsteadOfBatchDtoPatch()
        {
            Assert.False(DtoPatchPolicy.UseBatchDto(EmbyApiVersion.V4_11));
        }

        [Fact]
        public void ResolvePropertySetter_FindsNonPublicSetter()
        {
            var target = EmbyTargetResolver.ResolvePropertySetter(typeof(SetterTarget), nameof(SetterTarget.Value));

            Assert.True(target.IsFound);
            Assert.False(target.Method.IsStatic);
            Assert.True(target.Method.IsPrivate);
        }

        [Fact]
        public void ResolvePropertySetter_MissingPropertyIsStructuredFailure()
        {
            var target = EmbyTargetResolver.ResolvePropertySetter(typeof(SetterTarget), "Missing");

            Assert.False(target.IsFound);
            Assert.Contains("Missing", target.Description);
            Assert.False(string.IsNullOrWhiteSpace(target.FailureReason));
        }

        private sealed class SetterTarget
        {
            public string Value { get; private set; }
        }
    }
}
