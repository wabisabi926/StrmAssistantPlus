using StrmAssistant.Common;
using StrmAssistant.Core;
using Xunit;

namespace StrmAssistant.Tests
{
    public class ApiAvailabilityPolicyTests
    {
        [Fact]
        public void Emby410_MissingComponentsAreFailures()
        {
            Assert.False(ApiAvailabilityPolicy.MissingComponentsAreUnsupported(EmbyApiVersion.V4_10));
        }

        [Fact]
        public void Emby411_MissingComponentsAreKnownUnsupported()
        {
            Assert.True(ApiAvailabilityPolicy.MissingComponentsAreUnsupported(EmbyApiVersion.V4_11));
        }
    }
}
