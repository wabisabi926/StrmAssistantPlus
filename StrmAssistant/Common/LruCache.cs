using System;
using System.Collections.Generic;
using System.Threading;

namespace StrmAssistant.Common
{
    public class LruCache
    {
        private readonly int _capacity;
        private readonly TimeSpan? _ttl;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap;
        private readonly LinkedList<CacheEntry> _orderList;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private sealed class CacheEntry
        {
            public string Key;
            public object Value;
            public DateTime ExpiryUtc;
        }

        public LruCache(int capacity = 20, TimeSpan? ttl = null)
        {
            _capacity = capacity;
            _ttl = ttl;
            _cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>(capacity,
                StringComparer.OrdinalIgnoreCase);
            _orderList = new LinkedList<CacheEntry>();
        }

        public void AddOrUpdateCache<T>(string key, T value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_cacheMap.TryGetValue(key, out var existingNode))
                {
                    _orderList.Remove(existingNode);
                }
                else if (_cacheMap.Count >= _capacity)
                {
                    var leastUsed = _orderList.Last;
                    _orderList.RemoveLast();
                    _cacheMap.Remove(leastUsed.Value.Key);
                }

                var newNode = new LinkedListNode<CacheEntry>(new CacheEntry
                {
                    Key = key,
                    Value = value,
                    ExpiryUtc = _ttl.HasValue ? DateTime.UtcNow.Add(_ttl.Value) : DateTime.MaxValue
                });
                _orderList.AddFirst(newNode);
                _cacheMap[key] = newNode;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetFromCache<T>(string key, out T value) where T : class
        {
            _lock.EnterWriteLock();
            try
            {
                value = default;
                if (_cacheMap.TryGetValue(key, out var node))
                {
                    if (node.Value.ExpiryUtc <= DateTime.UtcNow)
                    {
                        _orderList.Remove(node);
                        _cacheMap.Remove(key);
                        return false;
                    }

                    _orderList.Remove(node);
                    _orderList.AddFirst(node);

                    value = node.Value.Value as T;
                    return true;
                }

                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
