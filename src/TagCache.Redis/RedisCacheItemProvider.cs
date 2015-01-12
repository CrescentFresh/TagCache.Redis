﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TagCache.Redis.Interfaces;

namespace TagCache.Redis
{
    public class RedisCacheItemProvider
    {
        private readonly ISerializationProvider _serializer;
        private readonly IRedisCacheItemFactory _cacheItemFactory;
        
        public RedisCacheItemProvider(ISerializationProvider serializer, IRedisCacheItemFactory cacheItemFactory)
        {
            _serializer = serializer;
            _cacheItemFactory = cacheItemFactory;
        }
        
        public async Task<IRedisCacheItem<T>> GetAsync<T>(RedisClient client, string key)
        {
            var cacheString = await client.GetAsync(key);

            if (!string.IsNullOrEmpty(cacheString))
            {
                return _serializer.Deserialize<IRedisCacheItem<T>>(cacheString.Value);
            }
            return null;
        }

        public IRedisCacheItem<T> Get<T>(RedisClient client, string key)
        {
            var cacheString = client.Get(key);

            if (!string.IsNullOrEmpty(cacheString))
            {
                return _serializer.Deserialize<IRedisCacheItem<T>>(cacheString.Value);
            }
            return null;
        }

        public List<IRedisCacheItem<T>> GetMany<T>(RedisClient client, string[] keys)
        {
            var result = new List<IRedisCacheItem<T>>();

            foreach (var key in keys)
            {
                var r = Get<T>(client, key);
                if (r != null)
                {
                    result.Add(r);
                }
            }

            return result;
        }

        public async Task<bool> SetAsync<T>(RedisClient client, T value, string key, DateTime expires, IEnumerable<string> tags)
        {
            if (value == null)
            {
                return false;
            }

            var cacheItem = Create(value, key, expires, tags);
            var serialized = _serializer.Serialize(cacheItem);
            int expirySeconds = GetExpirySeconds(expires);
            await client.SetAsync(key, serialized, expirySeconds);
            return true;
        }

        public bool Set<T>(RedisClient client, T value, string key, DateTime expires, IEnumerable<string> tags)
        {
            if (value == null)
            {
                return false;
            }

            var cacheItem = Create(value, key, expires, tags);
            var serialized = _serializer.Serialize(cacheItem);
            int expirySeconds = GetExpirySeconds(expires);
            client.Set(key, serialized, expirySeconds);
            return true;
        }

        private IRedisCacheItem<T> Create<T>(T value, string key, DateTime expires, IEnumerable<string> tags)
        {
            var tagsList = tags == null ? null : tags.ToList();

            var item = _cacheItemFactory.Create(
                key: key,
                tags: tagsList,
                expires: expires,
                value: value
            );

            return item;
        }

        /// <summary>
        /// calculates how long until the item expires, and adds additional time to allow the cache client to manually expire the item first and handle tags properly
        /// </summary>
        /// <param name="expires"></param>
        /// <returns></returns>
        private int GetExpirySeconds(DateTime expires)
        {
            var seconds = expires.Subtract(DateTime.Now).TotalSeconds;
            var result = (int)seconds;
            return Math.Max(1, result);
        }
    }
}
