using System.Text.Json;
using StackExchange.Redis;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Repositories;

public class RedisRepository
{
    private static readonly ConnectionMultiplexer RedisConnection =
        ConnectionMultiplexer.Connect(Configuration.RedisConnection);

    private readonly IDatabase _redis = RedisConnection.GetDatabase();

    public async Task<T?> Get<T>(string key)
    {
        var value = await _redis.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task<T?> Get<T>(string[] keys)
    {
        var values = await _redis.StringGetAsync(keys.Select(x => (RedisKey)x).ToArray());

        foreach (var value in values)
            if (value.HasValue)
                return JsonSerializer.Deserialize<T>(value!);

        return default;
    }

    public async Task Set<T>(string key, T value, TimeSpan? cacheTime = null)
    {
        await _redis.StringSetAsync(new RedisKey(key), JsonSerializer.Serialize(value),
            cacheTime ?? TimeSpan.FromSeconds(Configuration.RedisCacheLifeTime));
    }

    public async Task Set<T>(string[] keys, T value, TimeSpan? cacheTime = null)
    {
        foreach (var t in keys)
            await Set(t, value, cacheTime);
    }

    public async Task Remove(string key)
    {
        await _redis.KeyDeleteAsync(key);
    }

    public async Task<bool> SortedSetAdd(string key, int value, double score)
    {
        return await _redis.SortedSetAddAsync(key, value, score);
    }

    public async Task<long?> SortedSetRank(string key, int value)
    {
        return await _redis.SortedSetRankAsync(key, value, Order.Descending);
    }

    public async Task<bool> SortedSetRemove(string key, int value)
    {
        return await _redis.SortedSetRemoveAsync(key, value);
    }
}