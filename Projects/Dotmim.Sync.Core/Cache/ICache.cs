using System;

namespace DotmimSyncLegacy.Cache
{
    public interface ICache
    {
        void Clear();
        T GetValue<T>(string key);
        void Remove(string key);
        void Set<T>(string cacheKey, T value);
    }
}