using StrmAssistant.Core;
using StrmAssistant.Mod;
using Xunit;

namespace StrmAssistant.Tests
{
    public class ChineseSearchCompatibilityTests
    {
        [Theory]
        [InlineData("\"多段300\"*", "多段300")]
        [InlineData("^\"死侍與金剛狼\"*$", "死侍與金剛狼")]
        [InlineData("\"deadpool\" \"wolverine\"*", "deadpool wolverine")]
        [InlineData("duoduan", "duoduan")]
        [InlineData(null, null)]
        public void StripFtsExpressionWrappers_RemovesOnlyFtsSyntax(string input, string expected)
        {
            Assert.Equal(expected, EnhanceChineseSearch.StripFtsExpressionWrappers(input));
        }

        [Fact]
        public void ResolveByNameOrSignature_FindsTwoArgumentCreateSearchTerm()
        {
            var target = EmbyTargetResolver.ResolveByNameOrSignature(
                typeof(ChineseSearchCompatibilityTests),
                nameof(CreateSearchTerm),
                new[] { typeof(string), typeof(bool) },
                typeof(string),
                true);

            Assert.True(target.IsFound);
            Assert.Equal(2, target.Method.GetParameters().Length);
        }

        private static string CreateSearchTerm(string searchTerm, bool matchAnyWord) => searchTerm;
    }
}
