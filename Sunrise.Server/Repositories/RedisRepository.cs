using System.Text.Json;
using StackExchange.Redis;
using Sunrise.Server.Application;

namespace Sunrise.Server.Repositories;

public class RedisRepository
{
    private static readonly ConnectionMultiplexer RedisConnection =
        ConnectionMultiplexer.Connect(Configuration.RedisConnection);

    private static bool UseCache => Configuration.UseCache;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        IncludeFields = true
    };

    private readonly IDatabase _redis = RedisConnection.GetDatabase();

    public async Task<T?> Get<T>(string key)
    {
        if (!UseCache) return default;

        var value = await _redis.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!, JsonSerializerOptions) : default;
    }

    public async Task<T?> Get<T>(string[] keys)
    {
        if (!UseCache) return default;

        var values = await _redis.StringGetAsync(keys.Select(x => (RedisKey)x).ToArray());

        foreach (var value in values)
        {
            if (value.HasValue)
                return JsonSerializer.Deserialize<T>(value!, JsonSerializerOptions);
        }

        return default;
    }

    public async Task Set<T>(string key, T value, TimeSpan? cacheTime = null)
    {
        if (!UseCache) return;

        await _redis.StringSetAsync(new RedisKey(key),
            JsonSerializer.Serialize(value, JsonSerializerOptions),
            cacheTime ?? TimeSpan.FromSeconds(Configuration.RedisCacheLifeTime));
    }

    public async Task Set<T>(string[] keys, T value, TimeSpan? cacheTime = null)
    {
        if (!UseCache) return;

        foreach (var t in keys)
        {
            await Set(t, value, cacheTime);
        }
    }

    public async Task Remove(string key)
    {
        if (!UseCache) return;

        await _redis.KeyDeleteAsync(key);
    }

    public async Task Remove(string[] keys)
    {
        if (!UseCache) return;

        await _redis.KeyDeleteAsync(keys.Select(x => (RedisKey)x).ToArray());
    }

    public async Task<bool> SortedSetAdd(string key, int value, double score)
    {
        var timestamp = long.MaxValue - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Clear old values with the same key (because we have timestamp in key we can't just overwrite it)
        await SortedSetRemove(key, value);

        return await _redis.SortedSetAddAsync(key, $"{timestamp}:{value}", score);
    }

    public async Task<long?> SortedSetRank(string key, int value)
    {
        var entries = await _redis.SortedSetRangeByRankAsync(key);

        foreach (var entry in entries)
        {
            if (entry.ToString().EndsWith(":" + value))
            {
                var rank = await _redis.SortedSetRankAsync(key, entry, Order.Descending);
                return rank;
            }
        }

        return null;
    }

    public async Task<bool> SortedSetRemove(string key, int value)
    {
        var entries = await _redis.SortedSetRangeByRankAsync(key);

        foreach (var entry in entries)
        {
            if (entry.ToString().EndsWith(":" + value))
                return await _redis.SortedSetRemoveAsync(key, entry);
        }

        return false;
    }

    public void FlushAllCache()
    {
        _redis.Execute("FLUSHALL");
    }
}