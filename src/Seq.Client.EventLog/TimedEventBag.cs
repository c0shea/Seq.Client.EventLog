using System;
using Microsoft.Extensions.Caching.Memory;

// ReSharper disable UnusedMember.Global

namespace Seq.Client.EventLog
{
    public class TimedEventBag
    {
        private readonly MemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cachePolicy;

        /// <summary>
        ///     Cache objects that have already been seen, and expire them after X seconds
        /// </summary>
        /// <param name="expiration"></param>
        public TimedEventBag(int expiration)
        {
            expiration = expiration >= 0 ? expiration : 600;
            _cache = new MemoryCache(new MemoryCacheOptions {ExpirationScanFrequency = TimeSpan.FromSeconds(1)});
            _cachePolicy = new MemoryCacheEntryOptions {SlidingExpiration = TimeSpan.FromSeconds(expiration)};
        }

        public int Count => _cache.Count;

        public void Add(long? item)
        {
            if (item == null) return;
            if (!Contains(item))
                _cache.Set(((long) item).ToString(), (long) item, _cachePolicy);
        }

        public bool Contains(long? item)
        {
            return item != null && _cache.TryGetValue(((long) item).ToString(), out _);
        }
    }
}