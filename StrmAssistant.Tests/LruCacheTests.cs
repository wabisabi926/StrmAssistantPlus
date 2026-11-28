using StrmAssistant.Common;
using System;
using System.Threading;
using Xunit;

namespace StrmAssistant.Tests
{
    public class LruCacheTests
    {
        [Fact]
        public void ExpiredEntry_IsNotReturned()
        {
            var cache = new LruCache(2, TimeSpan.FromMilliseconds(20));
            cache.AddOrUpdateCache("key", "value");
            Thread.Sleep(50);

            Assert.False(cache.TryGetFromCache<string>("key", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void UnexpiredEntry_IsReturned()
        {
            var cache = new LruCache(2, TimeSpan.FromMinutes(1));
            cache.AddOrUpdateCache("key", "value");

            Assert.True(cache.TryGetFromCache<string>("key", out var value));
            Assert.Equal("value", value);
        }

        [Fact]
        public void Capacity_StillEvictsLeastRecentlyUsed()
        {
            var cache = new LruCache(2);
            cache.AddOrUpdateCache("a", "1");
            cache.AddOrUpdateCache("b", "2");
            Assert.True(cache.TryGetFromCache<string>("a", out _));
            cache.AddOrUpdateCache("c", "3");

            Assert.False(cache.TryGetFromCache<string>("b", out _));
            Assert.True(cache.TryGetFromCache<string>("a", out _));
            Assert.True(cache.TryGetFromCache<string>("c", out _));
        }
    }
}
