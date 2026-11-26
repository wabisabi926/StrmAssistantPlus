using System.Collections.Generic;
using System.Linq;
using StrmAssistant.Common;
using Xunit;

namespace StrmAssistant.Tests
{
    public class VersionMergePlannerTests
    {
        [Fact]
        public void Plan_IgnoresBlankProviderIds()
        {
            var items = new[]
            {
                new Candidate(1, new Dictionary<string, string> { ["tmdb"] = "" }),
                new Candidate(2, new Dictionary<string, string> { ["tmdb"] = "   " })
            };

            var plan = VersionMergePlanner.Plan(items, x => x.Id, x => x.ProviderIds);

            Assert.Empty(plan.Groups);
            Assert.Empty(plan.Conflicts);
        }

        [Fact]
        public void Plan_MergesConsistentProviderChain()
        {
            var items = new[]
            {
                new Candidate(1, new Dictionary<string, string> { ["tmdb"] = "100" }),
                new Candidate(2, new Dictionary<string, string> { ["tmdb"] = "100", ["imdb"] = "tt200" }),
                new Candidate(3, new Dictionary<string, string> { ["imdb"] = "tt200" })
            };

            var plan = VersionMergePlanner.Plan(items, x => x.Id, x => x.ProviderIds);

            var group = Assert.Single(plan.Groups);
            Assert.Equal(new long[] { 1, 2, 3 }, group.Select(x => x.Id).OrderBy(x => x));
            Assert.Empty(plan.Conflicts);
        }

        [Fact]
        public void Plan_SkipsProviderConflictChain()
        {
            var items = new[]
            {
                new Candidate(1, new Dictionary<string, string> { ["tmdb"] = "100" }),
                new Candidate(2, new Dictionary<string, string> { ["tmdb"] = "100", ["imdb"] = "tt200" }),
                new Candidate(3, new Dictionary<string, string> { ["tmdb"] = "999", ["imdb"] = "tt200" })
            };

            var plan = VersionMergePlanner.Plan(items, x => x.Id, x => x.ProviderIds);

            Assert.Empty(plan.Groups);
            var conflict = Assert.Single(plan.Conflicts);
            Assert.Equal("tmdb", conflict.ProviderKey, ignoreCase: true);
            Assert.Equal(new[] { "100", "999" }, conflict.Values.OrderBy(x => x));
            Assert.Equal(3, conflict.Items.Count);
        }

        [Fact]
        public void Plan_OrdersGroupByItemIdRegardlessOfInputOrder()
        {
            var items = new[]
            {
                new Candidate(3, new Dictionary<string, string> { ["tmdb"] = "100" }),
                new Candidate(1, new Dictionary<string, string> { ["tmdb"] = "100" }),
                new Candidate(2, new Dictionary<string, string> { ["tmdb"] = "100" })
            };

            var plan = VersionMergePlanner.Plan(items, x => x.Id, x => x.ProviderIds);

            var group = Assert.Single(plan.Groups);
            Assert.Equal(new long[] { 1, 2, 3 }, group.Select(x => x.Id));
        }

        [Fact]
        public void Plan_DeduplicatesRepeatedItemId()
        {
            var items = new[]
            {
                new Candidate(1, new Dictionary<string, string> { ["tmdb"] = "100" }),
                new Candidate(1, new Dictionary<string, string> { ["tmdb"] = "100" }),
                new Candidate(2, new Dictionary<string, string> { ["tmdb"] = "100" })
            };

            var plan = VersionMergePlanner.Plan(items, x => x.Id, x => x.ProviderIds);

            var group = Assert.Single(plan.Groups);
            Assert.Equal(new long[] { 1, 2 }, group.Select(x => x.Id).OrderBy(x => x));
        }

        private sealed class Candidate
        {
            public Candidate(long id, Dictionary<string, string> providerIds)
            {
                Id = id;
                ProviderIds = providerIds;
            }

            public long Id { get; }
            public Dictionary<string, string> ProviderIds { get; }
        }
    }
}
