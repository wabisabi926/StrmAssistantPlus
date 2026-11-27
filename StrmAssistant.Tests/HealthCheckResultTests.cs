using StrmAssistant.Core;
using Xunit;

namespace StrmAssistant.Tests
{
    public class HealthCheckResultTests
    {
        [Theory]
        [InlineData(HealthStatus.Healthy, HealthStatus.Unknown)]
        [InlineData(HealthStatus.Unknown, HealthStatus.Healthy)]
        public void AddCheck_HealthyAndUnknown_IsDegradedRegardlessOfOrder(
            HealthStatus firstStatus, HealthStatus secondStatus)
        {
            var result = new HealthCheckResult();

            result.AddCheck("EmbyVersionAdapter", firstStatus);
            result.AddCheck("HarmonyPatches", secondStatus);

            var unknownComponent = firstStatus == HealthStatus.Unknown
                ? "EmbyVersionAdapter"
                : "HarmonyPatches";
            Assert.Equal(HealthStatus.Degraded, result.OverallStatus);
            Assert.Equal(HealthStatus.Unknown, result.ComponentStatus[unknownComponent]);
            Assert.Contains($"{unknownComponent}: Unknown", result.Issues);
        }

        [Theory]
        [InlineData(HealthStatus.Degraded, true)]
        [InlineData(HealthStatus.Degraded, false)]
        [InlineData(HealthStatus.Unhealthy, true)]
        [InlineData(HealthStatus.Unhealthy, false)]
        [InlineData(HealthStatus.Critical, true)]
        [InlineData(HealthStatus.Critical, false)]
        public void AddCheck_UnknownAndMoreSevereStatus_PreservesStatusRegardlessOfOrder(
            HealthStatus severeStatus, bool unknownFirst)
        {
            var result = new HealthCheckResult();

            result.AddCheck("EmbyVersionAdapter", unknownFirst ? HealthStatus.Unknown : severeStatus);
            result.AddCheck("HarmonyPatches", unknownFirst ? severeStatus : HealthStatus.Unknown);

            Assert.Equal(severeStatus, result.OverallStatus);
        }
    }
}
