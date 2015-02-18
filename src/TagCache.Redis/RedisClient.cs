﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace TagCache.Redis
{
    public class RedisClient
    {
        private const bool _isRedisExpiryEnabled = true;
        private const string _rootName = "_redisCache";
        
        private readonly int _db;
        private readonly int _timeout = 100;
        private readonly RedisConnectionManager _connectionManager;


        public RedisClient(RedisConnectionManager connectionManager, int db, int timeout)
            : this(connectionManager)
        {
            _db = db;
            _timeout = timeout;
        }

        public RedisClient(RedisConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }


        public void Set(string key, RedisValue value, int expirySeconds)
        {
            var addTask = SetAsync(key, value, expirySeconds);
            addTask.Wait(_timeout);
            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }
            if (addTask.Result == false)
            {
                throw new Exception("Add Failed");
            }
        }

        public async Task<bool> SetAsync(string key, RedisValue value, int expirySeconds)
        {
            var conn = await _connectionManager.GetConnectionAsync();
            await conn.GetDatabase(_db).StringSetAsync(key, value, _isRedisExpiryEnabled ? (TimeSpan?)TimeSpan.FromSeconds(expirySeconds) : null).ConfigureAwait(false);

            return true;

        }

        public RedisValue? Get(string key)
        {
            var resultTask = GetAsync(key);
            resultTask.Wait(_timeout);
            if (resultTask.Exception != null)
            {
                throw resultTask.Exception;
            }

            return resultTask.Result;
        }

        public async Task<RedisValue?> GetAsync(string key)
        {
            var conn = _connectionManager.GetConnection();
            var value = await conn.GetDatabase(_db).StringGetAsync(key).ConfigureAwait(false);
            return value.HasValue ? (RedisValue?)value : null;
        }

        public bool Remove(string key)
        {
            return Remove(new[] { key });
        }

        public bool Remove(string[] keys)
        {
            var addTask = RemoveAsync(keys);
            addTask.Wait(_timeout);

            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }

            if (addTask.Result == false)
            {
                throw new Exception("Remove Failed");
            }

            return true;
        }

        public async Task<bool> RemoveAsync(string key)
        {
            return await RemoveAsync(new[] { key });
        }

        public async Task<bool> RemoveAsync(string[] keys)
        {
            var conn = await _connectionManager.GetConnectionAsync();
            await conn
                    .GetDatabase(_db)
                    .KeyDeleteAsync(keys.Select(key => (RedisKey)key).ToArray())
                    .ConfigureAwait(false);
            return true;
        }

        #region tags

        public string[] GetKeysForTag(string tag)
        {
            var conn = _connectionManager.GetConnection();
            return conn.GetDatabase(_db).SetMembers(TagKeysListKey(tag)).Select(r => !r.IsNullOrEmpty ? (string)r : null).ToArray();
        }

        public async Task<string[]> GetKeysForTagAsync(string tag)
        {
            var conn = await _connectionManager.GetConnectionAsync();
            return (await conn.GetDatabase(_db).SetMembersAsync(TagKeysListKey(tag))).Select(r => !r.IsNullOrEmpty ? (string)r : null).ToArray();
        }

        public void AddKeyToTags(string key, IEnumerable<string> tags)
        {
            var addTask = AddKeyToTagsAsync(key, tags);
            addTask.Wait(_timeout);
            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }
            if (addTask.Result == false)
            {
                throw new Exception("Add Tags Failed");
            }
        }

        public async Task<bool> AddKeyToTagsAsync(string key, IEnumerable<string> tags)
        {
            var enumerable = tags as string[] ?? tags.ToArray();
            if (key != null && tags != null && enumerable.Any())
            {

                var conn = await _connectionManager.GetConnectionAsync();

                var trans = conn
                                .GetDatabase(_db)
                                .CreateTransaction();

                foreach (var tag in enumerable)
                {
#pragma warning disable 4014
                    trans.SetAddAsync(TagKeysListKey(tag), key); //Don't await as tasks are only executed when transaction is executed
#pragma warning restore 4014
                }
                await trans.ExecuteAsync().ConfigureAwait(false);
            }
            return true;
        }

        public void RemoveKeyFromTags(string key, IEnumerable<string> tags)
        {
            var addTask = RemoveKeyFromTagsAsync(key, tags);
            addTask.Wait(_timeout);
            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }
            if (addTask.Result == false)
            {
                throw new Exception("Remove Tags Failed");
            }
        }

        public async Task<bool> RemoveKeyFromTagsAsync(string key, IEnumerable<string> tags)
        {
            var enumeratedTags = tags as string[] ?? tags.ToArray();
            if (tags != null && enumeratedTags.Any())
            {
                var conn = _connectionManager.GetConnection();
                var trans = conn.GetDatabase(_db).CreateTransaction();

                foreach (var tag in enumeratedTags)
                {
#pragma warning disable 4014
                    trans.SetRemoveAsync(TagKeysListKey(tag), key); //Don't await as tasks are only executed when transaction is executed
#pragma warning restore 4014
                }
                await trans.ExecuteAsync().ConfigureAwait(false);
            }
            return true;
        }

        public void SetTagsForKey(string key, IEnumerable<string> tags)
        {
            var addTask = SetTagsForKeyAsync(key, tags);
            addTask.Wait();
            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }
            if (addTask.Result == false)
            {
                throw new Exception("Set Tags For Key Failed");
            }
        }

        public async Task<bool> SetTagsForKeyAsync(string key, IEnumerable<string> tags)
        {
            if (key != null)
            {
                var conn = await _connectionManager.GetConnectionAsync();
                var trans = conn.GetDatabase(_db).CreateTransaction();

#pragma warning disable 4014
                trans.KeyDeleteAsync(KeyTagsListKey(key)); // Empty list. Don't await as all will be executed on transaction
#pragma warning restore 4014
                
                if (tags != null && tags.Any())
                {
#pragma warning disable 4014
                    trans.SetAddAsync(KeyTagsListKey(key), tags.Select(t => (RedisValue)t).ToArray()); // Add each tag. Don't await as all will be executed on transaction
#pragma warning restore 4014
                }

                await trans.ExecuteAsync().ConfigureAwait(false);
            }
            return true;
        }

        public void RemoveTagsForKey(string key)
        {
            var addTask = RemoveTagsForKeyAsync(key);
            addTask.Wait();
            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }
            if (addTask.Result == false)
            {
                throw new Exception("Set Tags For Key Failed");
            }
        }

        private async Task<bool> RemoveTagsForKeyAsync(string key)
        {
            if (key != null)
            {
                var conn = _connectionManager.GetConnection();

                await conn.GetDatabase(_db).KeyDeleteAsync(KeyTagsListKey(key)).ConfigureAwait(false); // empty list
            }
            return true;
        }

        public string[] GetTagsForKey(string key)
        {
            var addTask = GetTagsForKeyAsync(key);
            addTask.Wait(_timeout);
            if (addTask.Exception != null)
            {
                throw addTask.Exception;
            }
            return addTask.Result;
        }

        private async Task<string[]> GetTagsForKeyAsync(string key)
        {
            var conn = _connectionManager.GetConnection();
            var result = await conn.GetDatabase(_db).SetMembersAsync(KeyTagsListKey(key)).ConfigureAwait(false);
            return result
                    .Select(r => !r.IsNullOrEmpty ? r.ToString() : null)
                    .ToArray();
        }

        private string TagKeysListKey(string tag)
        {
            return string.Format("{0}:_cacheKeysByTag:{1}", _rootName, tag);
        }

        private string KeyTagsListKey(string key)
        {
            return string.Format("{0}:_cacheTagsByKey:{1}", _rootName, key);
        }

        #endregion

        #region TimeSets

        public bool SetTimeSet(string setKey, string value, DateTime date)
        {
            var task = SetTimeSetAsync(setKey, value, date);
            task.Wait(_timeout);
            if (task.Exception != null)
            {
                throw task.Exception;
            }
            if (task.Result == false)
            {
                throw new Exception("Set Failed");
            }
            return true;
        }
        public async Task<bool> SetTimeSetAsync(string setKey, string value, DateTime date)
        {
            var conn = await _connectionManager.GetConnectionAsync();
            return await conn.GetDatabase(_db).SortedSetAddAsync(setKey, value, Helpers.TimeToRank(date));
        }

        public bool RemoveTimeSet(string setKey, string[] keys)
        {
            var task = RemoveTimeSetAsync(setKey, keys);
            task.Wait(_timeout);
            if (task.Exception != null)
            {
                throw task.Exception;
            }
            if (task.Result == false)
            {
                throw new Exception("Remove Failed");
            }
            return true;
        }

        public async Task<bool> RemoveTimeSetAsync(string setKey, string[] keys)
        {
            var conn = await _connectionManager.GetConnectionAsync();
            await conn.GetDatabase(_db).SortedSetRemoveAsync(setKey, keys.Select(k => (RedisValue)k).ToArray());
            return true;
        }
        
        public async Task<string[]> GetFromTimeSetAsync(string setKey, DateTime maxDate)
        {
            var conn = await _connectionManager.GetConnectionAsync();
            var timeAsRank = Helpers.TimeToRank(maxDate);
            var keys = await conn.GetDatabase(_db).SortedSetRangeByScoreAsync(setKey, start: 0, stop: timeAsRank);
            return keys.Select(k => k.ToString()).ToArray();
        }

        public string[] GetFromTimeSet(string setKey, DateTime maxDate)
        {
            var conn = _connectionManager.GetConnection();
            var timeAsRank = Helpers.TimeToRank(maxDate);
            var keys = conn.GetDatabase(_db).SortedSetRangeByScore(setKey, start: 0, stop: timeAsRank);
            return keys.Select(k => k.ToString()).ToArray();
        }

        public void RemoveKey(string key)
        {
            var conn = _connectionManager.GetConnection();
            conn.GetDatabase(_db).KeyDelete(key);
        }

        #endregion

        public void Expire(string key, int seconds)
        {
            var conn = _connectionManager.GetConnection();
            conn.GetDatabase(_db).KeyExpire(key, TimeSpan.FromSeconds(seconds));
        }
    }
}
