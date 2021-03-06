﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using Fireasy.Common.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Fireasy.Common.Caching
{
    internal class MemoryStorage
    {
        internal static readonly ConcurrentDictionary<string, object> HashSet = new ConcurrentDictionary<string, object>();
        internal static readonly MemoryDictionary CacheDictionary = new MemoryDictionary();

        private static readonly CacheOptimizer _optimizer;
        private static readonly List<Action<CacheItem>> _events = new List<Action<CacheItem>>();

        static MemoryStorage()
        {
            _optimizer = new CacheOptimizer(CheckExpired);
        }

        /// <summary>
        /// 添加缓存移除的事件处理。
        /// </summary>
        /// <param name="handler"></param>
        internal static void AddCacheRemovedEventHandler(Action<CacheItem> handler)
        {
            _events.Add(handler);
        }

        /// <summary>
        /// 更新缓存项。
        /// </summary>
        /// <param name="cache"></param>
        /// <param name="checkExpired"></param>
        /// <returns></returns>
        internal static CacheItem UpdateEntry(CacheItem cache, bool checkExpired = true)
        {
            return _optimizer.Update(cache, checkExpired);
        }

        /// <summary>
        /// 检查字典的容量。
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="capacity"></param>
        internal static void CheckCapacity(string cacheKey, int capacity)
        {
            CacheDictionary.CheckCapacity(cacheKey, capacity, _optimizer.CurrentMaxGen, item =>
                {
                    _events.ForEach(s => s(item));
                });
        }

        /// <summary>
        /// 检查过期的缓存项。
        /// </summary>
        private static void CheckExpired()
        {
            foreach (var kvp in CacheDictionary)
            {
                if (kvp.Value != null && kvp.Value.HasExpired())
                {
                    if (CacheDictionary.TryRemove(kvp.Key, out CacheItem entry))
                    {
                        Tracer.Debug($"Remove MemoryCache '{kvp.Key}'.");

                        _events.ForEach(s => s(kvp.Value));
                        entry.TryDispose();
                    }
                }
            }
        }
    }
}
