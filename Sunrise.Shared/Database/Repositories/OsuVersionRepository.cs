using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Database.Repositories;

public class OsuVersionRepository(RedisRepository redis)
{

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public async Task<OsuVersion?> GetCachedVersion(string stream)
    {
        return await redis.Get<OsuVersion>(RedisKey.OsuVersionByStream(stream));
    }

    public async Task SetCachedVersion(string stream, OsuVersion version)
    {
        await redis.Set(RedisKey.OsuVersionByStream(stream), version, CacheTtl);
    }

    public async Task RemoveCachedVersion(string stream)
    {
        await redis.Remove(RedisKey.OsuVersionByStream(stream));
    }
}