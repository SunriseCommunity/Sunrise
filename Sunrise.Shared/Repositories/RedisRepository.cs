using System.Text.Json;
using CSharpFunctionalExtensions;
using StackExchange.Redis;
using Sunrise.Shared.Application;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Repositories;

public class RedisRepository(ConnectionMultiplexer redisConnection)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        IncludeFields = true
    };

    private readonly IDatabase _generalDatabase = redisConnection.GetDatabase(0);
    private readonly IDatabase _sortedSetsDatabase = redisConnection.GetDatabase(1);

    private static bool UseCache => Configuration.UseCache;

    public async Task<T?> Get<T>(string key)
    {
        if (!UseCache) return default;

        var value = await _generalDatabase.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!, JsonSerializerOptions) : default;
    }

    public async Task<T?> Get<T>(string[] keys)
    {
        if (!UseCache) return default;

        var values = await _generalDatabase.StringGetAsync(keys.Select(x => (RedisKey)x).ToArray());

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

        await _generalDatabase.StringSetAsync(new RedisKey(key),
            JsonSerializer.Serialize(value, JsonSerializerOptions),
            cacheTime ?? TimeSpan.FromSeconds(Configuration.RedisCacheLifeTime),
            flags: CommandFlags.FireAndForget);
    }

    public async Task Set<T>(string[] keys, T value, TimeSpan? cacheTime = null)
    {
        if (!UseCache) return;

        List<Task> setTasks = [];

        foreach (var t in keys)
        {
            setTasks.Add(Set(t, value, cacheTime));
        }

        await Task.WhenAll(setTasks);
    }

    public async Task Remove(string key)
    {
        if (!UseCache) return;

        await _generalDatabase.KeyDeleteAsync(key);
    }

    public async Task Remove(string[] keys)
    {
        if (!UseCache) return;

        await _generalDatabase.KeyDeleteAsync(keys.Select(x => (RedisKey)x).ToArray());
    }

    public async Task SortedSetAdd(string key, int value, double score)
    {
        var timestamp = long.MaxValue - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var redisValue = $"{timestamp}:{value}";

        await foreach (var entry in _sortedSetsDatabase.SortedSetScanAsync(key, $"*:{value}"))
        {
            await _sortedSetsDatabase.SortedSetRemoveAsync(key, entry.Element, CommandFlags.FireAndForget);
        }

        await _sortedSetsDatabase.SortedSetAddAsync(key, redisValue, score, CommandFlags.FireAndForget);
    }


    public async Task<Result<List<long>>> SortedSetRangeByRankAsync(string key, long start = 0, long stop = long.MaxValue)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var users = await _sortedSetsDatabase.SortedSetRangeByRankAsync(
                key,
                start,
                stop,
                Order.Descending
            );

            var userIds = users.Select(value => value.ToString().Split(":").Last());

            return userIds.Select(long.Parse).ToList();
        });
    }


    public async Task<long?> SortedSetRank(string key, int value)
    {
        await foreach (var entry in _sortedSetsDatabase.SortedSetScanAsync(key, $"*:{value}"))
        {
            return await _sortedSetsDatabase.SortedSetRankAsync(key, entry.Element, Order.Descending);
        }

        return null;
    }

    public async Task<long> SortedSetLength(string key)
    {
        return await _sortedSetsDatabase.SortedSetLengthAsync(key);
    }

    public async Task<bool> SortedSetRemove(string key, int value)
    {
        await foreach (var entry in _sortedSetsDatabase.SortedSetScanAsync(key, $"*:{value}"))
        {
            return await _sortedSetsDatabase.SortedSetRemoveAsync(key, entry.Element);
        }

        return false;
    }

    public async Task Flush(bool flushOnlyGeneralDatabase = true)
    {
        var server = redisConnection.GetServer(redisConnection.GetEndPoints().FirstOrDefault());

        if (flushOnlyGeneralDatabase)
        {
            await server.FlushDatabaseAsync(0);
            return;
        }

        await server.FlushAllDatabasesAsync();
    }
}