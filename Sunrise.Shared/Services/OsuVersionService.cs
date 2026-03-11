using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Services;

public class OsuVersionService(ILogger<OsuVersionService> logger, OsuVersionRepository repository, HttpClientService client)
{
    private static readonly SemaphoreSlim VersionRefreshLock = new(1, 1);

    public async Task<OsuVersion?> GetLatestVersion(string stream)
    {
        var cached = await repository.GetCachedVersion(stream);
        if (cached != null)
            return cached;

        await VersionRefreshLock.WaitAsync();
        try
        {
            cached = await repository.GetCachedVersion(stream);
            if (cached != null)
                return cached;

            await FetchAndCacheAllVersions();
        }
        finally
        {
            VersionRefreshLock.Release();
        }

        return await repository.GetCachedVersion(stream);
    }

    public async Task ForceRefreshVersions()
    {
        await VersionRefreshLock.WaitAsync();
        try
        {
            foreach (var stream in OsuVersion.SupportedStreams)
            {
                await repository.RemoveCachedVersion(stream);
            }

            await FetchAndCacheAllVersions();
        }
        finally
        {
            VersionRefreshLock.Release();
        }
    }

    private async Task FetchAndCacheAllVersions()
    {
        var serverSession = BaseSession.GenerateServerSession();
        var changelogResult = await client.SendRequest<ChangelogResponse>(serverSession, ApiType.GetOsuChangelog, []);

        if (changelogResult.IsFailure)
        {
            logger.LogWarning("Failed to fetch osu! changelog from remote server. Message: {Message}", changelogResult.Error.Message);
            return;
        }

        var changelog = changelogResult.Value;

        if (changelog.Streams.Count == 0)
        {
            logger.LogWarning("No streams found in osu! changelog response.");
            return;
        }

        foreach (var stream in OsuVersion.SupportedStreams)
        {
            var version = ExtractLatestVersion(stream, changelog);

            if (version == null)
            {
                logger.LogWarning("Failed to extract latest version for stream {Stream} from changelog", stream);
                continue;
            }

            await repository.SetCachedVersion(stream, version);
        }
    }

    private static OsuVersion? ExtractLatestVersion(string stream, ChangelogResponse changelog)
    {

        var streamChangelog = changelog.Streams.FirstOrDefault(s => s.Name.Equals(stream, StringComparison.OrdinalIgnoreCase));

        if (streamChangelog == null)
        {
            return null;
        }

        var version = OsuVersion.Parse(stream, streamChangelog.LatestBuild.Version);

        return version;
    }
}