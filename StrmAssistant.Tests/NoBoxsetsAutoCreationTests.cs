using StrmAssistant.Mod;
using System.Reflection;
using Xunit;

namespace StrmAssistant.Tests
{
    public class NoBoxsetsAutoCreationTests
    {
        [Fact]
        public void FindParameterlessVoidMethod_FindsUniquePrivateTarget()
        {
            var method = NoBoxsetsAutoCreation.FindParameterlessVoidMethod(typeof(UniqueTarget));

            Assert.NotNull(method);
            Assert.Equal("EnsureLibraryFolder", method.Name);
        }

        [Fact]
        public void FindParameterlessVoidMethod_RejectsAmbiguousTargets()
        {
            var method = NoBoxsetsAutoCreation.FindParameterlessVoidMethod(typeof(AmbiguousTarget));

            Assert.Null(method);
        }

        [Fact]
        public void FindParameterlessVoidMethod_IgnoresPropertyAccessors()
        {
            var method = NoBoxsetsAutoCreation.FindParameterlessVoidMethod(typeof(GetterOnlyTarget));

            Assert.Null(method);
        }

        private class UniqueTarget
        {
            private void EnsureLibraryFolder() { }
        }

        private class AmbiguousTarget
        {
            private void First() { }
            private void Second() { }
        }

        private class GetterOnlyTarget
        {
            private bool Value => true;
        }
    }
}
