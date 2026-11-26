using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using StrmAssistant.Mod;
using System;
using Xunit;

namespace StrmAssistant.Tests
{
    public class UnlockIntroSkipTests
    {
        [Fact]
        public void ShouldForceDetectionSupport_OnlyForShortcuts()
        {
            Assert.False(UnlockIntroSkip.ShouldForceDetectionSupport(null));
            Assert.False(UnlockIntroSkip.ShouldForceDetectionSupport(new Video()));
            Assert.True(UnlockIntroSkip.ShouldForceDetectionSupport(new Video { Path = "D:\\TV\\episode.strm" }));
        }

        [Fact]
        public void ShouldForceCreditsDetectionSupport_RespectsLibraryOption()
        {
            var shortcut = new Video { Path = "D:\\TV\\episode.strm" };

            Assert.False(UnlockIntroSkip.ShouldForceCreditsDetectionSupport(shortcut, false));
            Assert.True(UnlockIntroSkip.ShouldForceCreditsDetectionSupport(shortcut, true));
            Assert.False(UnlockIntroSkip.ShouldForceCreditsDetectionSupport(new Video(), true));
        }

        [Fact]
        public void ApplyDetectionScope_SetsLibraryPathsAndBlacklist()
        {
            var query = new InternalItemsQuery();
            var paths = new[] { "D:\\TV\\" };
            var blacklist = new long[] { 10, 20 };

            UnlockIntroSkip.ApplyDetectionScope(query, paths, blacklist, false, new long[0]);

            Assert.Equal(paths, query.PathStartsWithAny);
            Assert.Equal(blacklist, query.ExcludeParentIds);
        }

        [Fact]
        public void ApplyDetectionScope_PreservesExistingFiltersWhenScopeIsEmpty()
        {
            var paths = new[] { "D:\\Native\\" };
            var blacklist = new long[] { 99 };
            var query = new InternalItemsQuery
            {
                PathStartsWithAny = paths,
                ExcludeParentIds = blacklist
            };
            Assert.Same(paths, query.PathStartsWithAny);

            UnlockIntroSkip.ApplyDetectionScope(query, new string[0], new long[0], false, new long[0]);

            Assert.Same(paths, query.PathStartsWithAny);
            Assert.Same(blacklist, query.ExcludeParentIds);
        }

        [Fact]
        public void ApplyDetectionScope_FavoritesUsesSeasonParents()
        {
            var query = new InternalItemsQuery();
            var favorites = new long[] { 30, 40 };

            UnlockIntroSkip.ApplyDetectionScope(query, new[] { "D:\\TV\\" }, new long[0], true, favorites);

            Assert.Equal(favorites, query.ParentIds);
            Assert.Empty(query.PathStartsWithAny);
        }

        [Fact]
        public void ApplyDetectionScope_EmptyFavoritesDoesNotBecomeGlobal()
        {
            var query = new InternalItemsQuery();

            UnlockIntroSkip.ApplyDetectionScope(query, new[] { "D:\\TV\\" }, new long[0], true, new long[0]);

            Assert.Equal(new long[] { -1 }, query.ParentIds);
        }
    }
}
