using MediaBrowser.Controller.Entities.Movies;
using StrmAssistant.Web.Service;
using Xunit;

namespace StrmAssistant.Tests
{
    public class VersionDeletePlannerTests
    {
        [Fact]
        public void Build_DeniesPlanWhenRootIsNotAuthorized()
        {
            var root = new Movie();

            var plan = VersionDeletePlanner.Build(root, new[] { root }, _ => false);

            Assert.True(plan.IsDenied);
            Assert.Empty(plan.Authorized);
            Assert.Equal(0, plan.UnauthorizedCount);
        }

        [Fact]
        public void Build_CountsUnauthorizedCandidatesWithoutDeletingThem()
        {
            var root = new Movie();
            var allowed = new Movie();
            var denied = new Movie();

            var plan = VersionDeletePlanner.Build(root, new[] { root, allowed, denied },
                item => !ReferenceEquals(item, denied));

            Assert.False(plan.IsDenied);
            Assert.Equal(2, plan.Authorized.Count);
            Assert.Contains(root, plan.Authorized);
            Assert.Contains(allowed, plan.Authorized);
            Assert.DoesNotContain(denied, plan.Authorized);
            Assert.Equal(1, plan.UnauthorizedCount);
        }

        [Fact]
        public void Build_DoesNotDuplicateRoot()
        {
            var root = new Movie();

            var plan = VersionDeletePlanner.Build(root, new[] { root, root }, _ => true);

            Assert.Single(plan.Authorized);
            Assert.Same(root, plan.Authorized[0]);
        }
    }
}
