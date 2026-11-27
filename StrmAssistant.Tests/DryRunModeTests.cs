using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class DryRunModeTests
    {
        [Fact]
        public void Skip_WhenDisabled_AllowsWrite()
        {
            Assert.False(DryRunMode.Skip("test write", false));
        }

        [Fact]
        public void Skip_WhenEnabled_BlocksWrite()
        {
            Assert.True(DryRunMode.Skip("test write", true));
        }
    }
}
