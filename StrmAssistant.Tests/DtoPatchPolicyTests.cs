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
    }
}
