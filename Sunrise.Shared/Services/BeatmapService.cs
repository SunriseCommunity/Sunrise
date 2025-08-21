using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Services;

public class BeatmapService(ILogger<BeatmapService> logger, DatabaseService database, HttpClientService client)
{
    private readonly SemaphoreSlim _dbSemaphore = new(1);

    public async Task<Result<BeatmapSet, ErrorMessage>> GetBeatmapSet(BaseSession session, int? beatmapSetId = null,
        string? beatmapHash = null, int? beatmapId = null, int? retryCount = 1, CancellationToken ct = default)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null)
            return Result.Failure<BeatmapSet, ErrorMessage>(new ErrorMessage
            {
                Message = "No proper ids were specified.",
                Status = HttpStatusCode.BadRequest
            });

        var beatmapSet = await database.Beatmaps.GetCachedBeatmapSet(beatmapSetId, beatmapHash, beatmapId);
        if (beatmapSet != null) return beatmapSet;

        var beatmapSetTask = Result.Failure<BeatmapSet, ErrorMessage>(new ErrorMessage
        {
            Message = "Could not retrieve beatmap set.",
            Status = HttpStatusCode.BadRequest
        });

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

        while (retryCount > 0 && !linkedCts.IsCancellationRequested && !IsValidResult(beatmapSetTask))
        {
            retryCount--;

            if (beatmapId != null)
                beatmapSetTask = await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId], ct: linkedCts.Token);

            if (beatmapHash != null && !IsValidResult(beatmapSetTask))
                beatmapSetTask = await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash], ct: linkedCts.Token);

            if (beatmapSetId != null && !IsValidResult(beatmapSetTask))
                beatmapSetTask = await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId], ct: linkedCts.Token);

            if (!IsValidResult(beatmapSetTask) && !linkedCts.IsCancellationRequested)
            {
                logger.LogWarning($"Error while getting beatmap set: {beatmapSetTask.Error.Message}, Retry count: {retryCount}");

                if (retryCount > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), linkedCts.Token);
                }
            }
        }

        if (beatmapSetTask.IsFailure)
            return beatmapSetTask;

        beatmapSet = beatmapSetTask.Value;

        await database.Beatmaps.SetCachedBeatmapSet(beatmapSet);

        try
        {
            await _dbSemaphore.WaitAsync(linkedCts.Token);

            var customStatuses = await database.Beatmaps.CustomStatuses.GetCustomBeatmapSetStatuses(beatmapSet.Id,
                new QueryOptions(true)
                {
                    QueryModifier = q => q.Cast<CustomBeatmapStatus>().IncludeBeatmapNominator()
                },
                linkedCts.Token);

            beatmapSet.UpdateBeatmapRanking(customStatuses);
        }
        finally
        {
            _dbSemaphore.Release();
        }

        return beatmapSet;
    }

    public async Task<Result<List<BeatmapSet>, ErrorMessage>> GetBeatmapSets(BaseSession session, List<int> beatmapSetIds, CancellationToken ct = default)
    {
        var beatmapSetLookup = beatmapSetIds.ToLookup(id => id);

        var beatmapSetsTasks = beatmapSetLookup.Select(g =>
        {
            var beatmapSetId = g.Key;

            return GetBeatmapSet(session, beatmapSetId, ct: ct);
        });

        var beatmapSetsResults = await Task.WhenAll(beatmapSetsTasks);

        if (beatmapSetsResults.Any(b => b.IsFailure))
        {
            return beatmapSetsResults.First(v => v.IsFailure).Error;
        }

        var beatmapSets = beatmapSetsResults.Select(v => v.Value);

        return beatmapSets.ToList();
    }

    public async Task<Result<List<BeatmapSet>, ErrorMessage>> SearchBeatmapSets(BaseSession session, string? rankedStatus, string mode,
        string query, Pagination pagination, CancellationToken ct = default)
    {
        var beatmapSetsResult = await client.SendRequest<List<BeatmapSet>>(session,
            ApiType.BeatmapSetSearch,
            [query, pagination.PageSize, pagination.Page * pagination.PageSize, rankedStatus, mode],
            ct: ct);

        if (beatmapSetsResult.IsFailure)
            return beatmapSetsResult;

        var beatmapSets = beatmapSetsResult.Value;

        if (beatmapSets == null) return new List<BeatmapSet>();

        foreach (var set in beatmapSets)
        {
            var customStatuses = await database.Beatmaps.CustomStatuses.GetCustomBeatmapSetStatuses(set.Id,
                new QueryOptions(true)
                {
                    QueryModifier = q => q.Cast<CustomBeatmapStatus>().Include(x => x.UpdatedByUser)
                },
                ct);

            set.UpdateBeatmapRanking(customStatuses);
        }

        return beatmapSets;
    }

    public async Task<Result<CustomBeatmapStatus?>> ChangeBeatmapCustomStatus(User user, Beatmap beatmap, BeatmapStatusWeb? newStatus, bool? resetCustomStatus)
    {
        if (newStatus == null && resetCustomStatus == null)
            return Result.Failure<CustomBeatmapStatus?>("No proper status arguments were specified.");

        var isCanChangeBeatmapStatus = user.Privilege.HasFlag(UserPrivilege.Bat);

        if (!isCanChangeBeatmapStatus)
        {
            return Result.Failure<CustomBeatmapStatus?>("User cannot change beatmap status.");
        }

        var customStatus = await database.Beatmaps.CustomStatuses.GetCustomBeatmapStatus(beatmap.Checksum!);

        if (resetCustomStatus.HasValue)
        {
            if (customStatus != null)
            {
                await database.Beatmaps.CustomStatuses.DeleteCustomBeatmapStatus(customStatus);
            }

            return null;
        }

        if (newStatus.HasValue)
        {
            if (customStatus != null)
            {
                customStatus.Status = newStatus.Value;
                customStatus.UpdatedByUserId = user.Id;

                var updateCustomStatusResult = await database.Beatmaps.CustomStatuses.UpdateCustomBeatmapStatus(customStatus);
                return updateCustomStatusResult.IsFailure ? Result.Failure<CustomBeatmapStatus?>(updateCustomStatusResult.Error) : customStatus;
            }

            customStatus = new CustomBeatmapStatus
            {
                Status = newStatus.Value,
                UpdatedByUserId = user.Id,
                BeatmapHash = beatmap.Checksum!,
                BeatmapSetId = beatmap.BeatmapsetId
            };

            var addCustomStatusResult = await database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(customStatus);
            return addCustomStatusResult.IsFailure ? Result.Failure<CustomBeatmapStatus?>(addCustomStatusResult.Error) : customStatus;
        }

        return Result.Failure<CustomBeatmapStatus?>("Unknown error occurred while changing beatmap status.");
    }

    private bool IsValidResult(Result<BeatmapSet, ErrorMessage> result)
    {
        var isNotFoundResult = result.IsFailure && result.Error.Status == HttpStatusCode.NotFound;
        var isValidResult = result.IsSuccess || isNotFoundResult;

        return isValidResult;
    }
}