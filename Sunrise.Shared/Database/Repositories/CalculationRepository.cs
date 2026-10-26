using System.Data;
using EFCoreSecondLevelCacheInterceptor;
using EntityFrameworkCore.Locking;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;

namespace Sunrise.Shared.Database.Repositories;

public class CalculationRepository(SunriseDbContext dbContext)
{
    public const int BeatmapChecksPerTick = 50;
    public static readonly TimeSpan BeatmapCheckInterval = TimeSpan.FromDays(1);

    public async Task<int?> GetOrCreateVersionId(string? rosuVersion, CancellationToken ct = default)
    {
        if (rosuVersion == null)
            return null;

        var query = dbContext.CalculationVersions.NotCacheable()
            .Where(v => v.RosuVersion == rosuVersion && v.SunriseRevision == CalculationVersion.CurrentSunriseRevision)
            .Select(v => (int?)v.Id);

        var id = await query.FirstOrDefaultAsync(ct);
        if (id != null)
            return id.Value;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT IGNORE INTO calculation_version (RosuVersion, SunriseRevision) VALUES ({rosuVersion}, {CalculationVersion.CurrentSunriseRevision})", ct);

        return await query.FirstAsync(ct);
    }

    public async Task<CalculationRunPhase?> GetFrozenPhase(CancellationToken ct = default)
    {
        return await dbContext.CalculationRuns.NotCacheable()
            .Where(r => r.FinishedAt == null)
            .Select(r => (CalculationRunPhase?)r.Phase)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CalculationRun?> GetLastRun(CancellationToken ct = default)
    {
        return await dbContext.CalculationRuns.NotCacheable()
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CalculationRun> StartRun(int targetVersionId, bool isForced = false, CancellationToken ct = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        foreach (var unfinishedRun in await dbContext.CalculationRuns.NotCacheable().Where(r => r.FinishedAt == null).ForUpdate().ToListAsync(ct))
        {
            unfinishedRun.IsSuperseded = true;
            await StopRun(unfinishedRun, ct);
        }

        var run = new CalculationRun
        {
            TargetVersionId = targetVersionId,
            Phase = CalculationRunPhase.Enqueue,
            StartedAt = DateTime.UtcNow,
            IsForced = isForced
        };

        dbContext.CalculationRuns.Add(run);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return run;
    }

    public async Task StopRun(CalculationRun run, CancellationToken ct = default)
    {
        run.FinishedAt = DateTime.UtcNow;

        await dbContext.ScoreProcessingTasks
            .Where(t => t.RunId == run.Id && t.Status == ScoreProcessingStatus.Pending)
            .ExecuteDeleteAsync(ct);

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<ScoreProcessingStatus, int>> CountRunTasks(CalculationRun run, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks.NotCacheable()
            .Where(t => t.RunId == run.Id)
            .GroupBy(t => t.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
    }

    public async Task<int> EnqueueRunScores(CalculationRun run, CancellationToken ct = default)
    {
        return await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT IGNORE INTO score_processing_task (TaskType, ScoreId, RunId, Priority, Status, RetryCount, CreatedAt)
            SELECT {(int)ScoreTaskType.Recalculation}, s.Id, {run.Id}, {(int)ScoreProcessingPriority.Normal}, {(int)ScoreProcessingStatus.Pending}, 0, UTC_TIMESTAMP()
            FROM score s
            JOIN beatmap_hash_status h ON h.BeatmapHash = s.BeatmapHash
            WHERE s.SubmissionStatus <> {(int)SubmissionStatus.Deleted}
              AND h.Status <> {(int)BeatmapStatus.NotSubmitted}
              AND ({run.IsForced} OR s.CalculationVersionId IS NULL OR s.CalculationVersionId <> {run.TargetVersionId})
              AND NOT EXISTS (SELECT 1 FROM score_processing_task t WHERE t.ScoreId = s.Id AND t.RunId = {run.Id})",
            ct);
    }

    public async Task<bool> HasOutdatedScores(CalculationRun run, CancellationToken ct = default)
    {
        return await dbContext.Scores.NotCacheable()
            .AnyAsync(s => s.SubmissionStatus != SubmissionStatus.Deleted
                           && s.BeatmapHashStatus!.Status != BeatmapStatus.NotSubmitted
                           && (s.CalculationVersionId == null || s.CalculationVersionId != run.TargetVersionId)
                           && !dbContext.ScoreProcessingTasks.Any(t => t.ScoreId == s.Id && t.RunId == run.Id), ct);
    }

    public async Task<bool> HasActiveRunTasks(CalculationRun run, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks.NotCacheable()
            .Where(t => t.RunId == run.Id)
            .FilterInProgressTasks()
            .AnyAsync(ct);
    }

    public async Task<BeatmapHashStatus?> GetBeatmapHashStatus(string beatmapHash, CancellationToken ct = default)
    {
        return await dbContext.BeatmapHashStatuses.NotCacheable()
            .FirstOrDefaultAsync(h => h.BeatmapHash == beatmapHash, ct);
    }

    public async Task<BeatmapHashStatus?> LockBeatmapHashStatus(string beatmapHash, BeatmapStatus? newStatus = null, CancellationToken ct = default)
    {
        foreach (var staleEntry in dbContext.ChangeTracker.Entries<BeatmapHashStatus>().Where(e => e.Entity.BeatmapHash == beatmapHash).ToList())
            staleEntry.State = EntityState.Detached;

        var query = dbContext.BeatmapHashStatuses.NotCacheable().Where(h => h.BeatmapHash == beatmapHash);
        var isStatusChanging = newStatus != null && await query.AsNoTracking().AnyAsync(h => h.Status != newStatus, ct);

        return await (isStatusChanging ? query.ForUpdate() : query.ForShare()).FirstOrDefaultAsync(ct);
    }

    public async Task<BeatmapHashStatus> ApplyBeatmapHashStatus(string beatmapHash, int beatmapId, BeatmapStatus status, CancellationToken ct = default)
    {
        await using var transaction = dbContext.Database.CurrentTransaction == null ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;

        var beatmapHashStatus = await LockBeatmapHashStatus(beatmapHash, status, ct);
        var previousStatus = beatmapHashStatus?.Status;

        if (beatmapHashStatus != null && previousStatus != status && await HasPendingBeatmapStatusChanges(beatmapHash, ct))
            return beatmapHashStatus;

        if (beatmapHashStatus == null)
        {
            beatmapHashStatus = new BeatmapHashStatus
            {
                BeatmapHash = beatmapHash,
                CheckedAt = DateTime.UtcNow
            };

            dbContext.BeatmapHashStatuses.Add(beatmapHashStatus);
        }
        else if (previousStatus != status)
        {
            beatmapHashStatus.PreviousStatus = previousStatus;
        }

        beatmapHashStatus.BeatmapId = beatmapId;
        beatmapHashStatus.Status = status;
        beatmapHashStatus.MissCount = 0;
        await dbContext.SaveChangesAsync(ct);

        if (previousStatus != null && (previousStatus.Value.IsScoreable() != status.IsScoreable() || previousStatus.Value.IsRanked() != status.IsRanked()))
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO score_processing_task (TaskType, ScoreId, Priority, Status, RetryCount, CreatedAt)
                SELECT {(int)ScoreTaskType.BeatmapStatusChange}, MIN(s.Id), {(int)ScoreProcessingPriority.Medium}, {(int)ScoreProcessingStatus.Pending}, 0, UTC_TIMESTAMP()
                FROM score s
                WHERE s.BeatmapHash = {beatmapHash} AND s.SubmissionStatus <> {(int)SubmissionStatus.Deleted}
                GROUP BY s.UserId, s.GameMode",
                ct);

        if (transaction != null)
            await transaction.CommitAsync(ct);

        return beatmapHashStatus;
    }

    public async Task<bool> HasPendingBeatmapStatusChanges(string beatmapHash, CancellationToken ct = default)
    {
        return await dbContext.ScoreProcessingTasks.NotCacheable()
            .AnyAsync(t => t.TaskType == ScoreTaskType.BeatmapStatusChange && t.Status != ScoreProcessingStatus.Failed
                           && dbContext.Scores.Any(s => s.Id == t.ScoreId && s.BeatmapHash == beatmapHash), ct);
    }

    public async Task MarkBeatmapCheckDue(string beatmapHash, CancellationToken ct = default)
    {
        await dbContext.BeatmapHashStatuses
            .Where(h => h.BeatmapHash == beatmapHash)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.CheckedAt, DateTime.UnixEpoch), ct);
    }

    public IQueryable<BeatmapHashStatus> GetDueBeatmapChecks()
    {
        var checkedBefore = DateTime.UtcNow - BeatmapCheckInterval;

        return dbContext.BeatmapHashStatuses.NotCacheable()
            .Where(h => h.CheckedAt < checkedBefore)
            .OrderBy(h => h.CheckedAt)
            .ThenBy(h => h.Id);
    }
}
