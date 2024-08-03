using System.Text.Json;
using StackExchange.Redis;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Repositories;

public class RedisRepository
{

    private static readonly ConnectionMultiplexer RedisConnection = ConnectionMultiplexer.Connect(Configuration.RedisConnection);
    public readonly IDatabase Redis = RedisConnection.GetDatabase();

    public async Task<T?> Get<T>(string key)
    {
        var value = await Redis.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task Set<T>(string key, T value, TimeSpan? cacheTime = null)
    {
        await Redis.StringSetAsync(key, JsonSerializer.Serialize(value), cacheTime ?? TimeSpan.FromMinutes(15));
    }

    public async Task Remove(string key)
    {
        await Redis.KeyDeleteAsync(key);
    }

    public async Task<bool> SortedSetAdd(string key, string value, double score)
    {
        return await Redis.SortedSetAddAsync(key, value, score);
    }

    public async Task<double?> SortedSetScore(string key, string value)
    {
        return await Redis.SortedSetScoreAsync(key, value);
    }

    public async Task<bool> SortedSetRemove(string key, string value)
    {
        return await Redis.SortedSetRemoveAsync(key, value);
    }
}