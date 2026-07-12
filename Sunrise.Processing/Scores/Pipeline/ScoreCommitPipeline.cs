using System.Data;
using CSharpFunctionalExtensions;
using Sunrise.Processing.Scores.Processors;
using Sunrise.Shared.Application;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using LocalProperties = Sunrise.Shared.Database.Models.LocalProperties;

namespace Sunrise.Processing.Scores.Pipeline;

[TraceExecution]
public class ScoreCommitPipeline
{
    private readonly DatabaseService _database;
    private readonly IScoreEntityProcessor[] _processors;

    public ScoreCommitPipeline(DatabaseService database, IEnumerable<IScoreEntityProcessor> processors)
    {
        _database = database;
        _processors = processors.OrderBy(p => p.Priority).ToArray();
    }

    public async Task<Result> Commit(
        ScoreCommitContext ctx,
        ScoreProcessingTask? task,
        CancellationToken ct)
    {
        var commitResult = await CommitPrepared(ScorePrepareContext.FromCommitContext(ctx), task, ct);
        if (commitResult.IsFailure)
            return Result.Failure(commitResult.Error);

        var committed = commitResult.Value;
        ctx.OriginalState = committed.OriginalState;
        ctx.PreviousUserStatsSnapshot = committed.PreviousUserStatsSnapshot;
        ctx.UserPersonalBestScores = committed.UserPersonalBestScores;
        ctx.UnlockedMedals = committed.UnlockedMedals;
        ctx.Score = committed.Score;
        ctx.UserStats = committed.UserStats;
        ctx.UserGrades = committed.UserGrades;

        return Result.Success();
    }

    public async Task<Result<ScoreCommitContext>> CommitPrepared(
        ScorePrepareContext prepareCtx,
        ScoreProcessingTask? task,
        CancellationToken ct)
    {
        ScoreCommitContext? committedCtx = null;

        var transactionResult = await _database.CommitAsTransactionAsync(async () => { committedCtx = await ExecuteCommitAsync(prepareCtx, task, ct); },
            ct,
            IsolationLevel.ReadCommitted);

        if (transactionResult.IsFailure)
            return Result.Failure<ScoreCommitContext>(transactionResult.Error);

        if (committedCtx == null)
            return Result.Failure<ScoreCommitContext>("Score commit context was not created during transaction.");

        return committedCtx;
    }

    private async Task<ScoreCommitContext> ExecuteCommitAsync(
        ScorePrepareContext prepareCtx,
        ScoreProcessingTask? task,
        CancellationToken ct)
    {
        var preparedScore = prepareCtx.UntrackedScore
                            ?? throw new ApplicationException("Score prepare context did not contain an untracked score.");
        var user = preparedScore.User ?? await _database.Users.GetUser(preparedScore.UserId, ct: ct)
            ?? throw new ApplicationException($"User {preparedScore.UserId} was not found while locking score commit state");

        var lockedStats = await _database.Users.Stats.LockUserStatsForUpdate(new UserStats
            {
                UserId = preparedScore.UserId,
                GameMode = preparedScore.GameMode
            },
            ct);

        if (lockedStats == null)
        {
            var createdStats = await _database.Users.Stats.GetUserStats(preparedScore.UserId, preparedScore.GameMode, ct);
            if (createdStats != null)
                lockedStats = await _database.Users.Stats.LockUserStatsForUpdate(createdStats, ct);
        }

        if (lockedStats == null)
        {
            throw new ApplicationException(
                $"User stats for user {preparedScore.UserId} and mode {preparedScore.GameMode} were not found while locking score commit state");
        }

        var lockedGrades = await _database.Users.Grades.LockUserGradesForUpdate(new UserGrades
            {
                UserId = preparedScore.UserId,
                GameMode = preparedScore.GameMode
            },
            ct);

        if (lockedGrades == null)
        {
            var createdGrades = await _database.Users.Grades.GetUserGrades(preparedScore.UserId, preparedScore.GameMode, ct);
            if (createdGrades != null)
                lockedGrades = await _database.Users.Grades.LockUserGradesForUpdate(createdGrades, ct);
        }

        if (lockedGrades == null)
        {
            throw new ApplicationException(
                $"User grades for user {preparedScore.UserId} and mode {preparedScore.GameMode} were not found while locking score commit state");
        }

        var (currentRank, _) = await _database.Users.Stats.Ranks.GetUserRanks(user, lockedStats.GameMode, false, ct);
        lockedStats.LocalProperties.Rank = currentRank;

        var targetScoreId = prepareCtx.TaskType == ScoreTaskType.Submission ? (int?)null : preparedScore.Id;
        var (lockedScore, peers) = await _database.Scores.GetUserScoreByIdWithBeatmapPeersForUpdate(
            preparedScore.UserId,
            preparedScore.BeatmapHash,
            preparedScore.GameMode,
            preparedScore.Mods,
            targetScoreId,
            ct);

        var score = prepareCtx.TaskType == ScoreTaskType.Submission
            ? preparedScore
            : lockedScore ?? throw new ApplicationException($"Score {preparedScore.Id} was not found while locking score commit target");

        var originalState = ScoreStateSnapshot.Capture(score);

        if (prepareCtx.TaskType != ScoreTaskType.Submission && prepareCtx.NewScorePerformancePointsValue.HasValue)
            score.PerformancePoints = prepareCtx.NewScorePerformancePointsValue.Value;

        var ctx = new ScoreCommitContext(
            prepareCtx.TaskType,
            score,
            user,
            lockedStats,
            lockedGrades,
            prepareCtx.Beatmap,
            prepareCtx.BeatmapSet)
        {
            OriginalState = originalState,
            PreviousUserStatsSnapshot = lockedStats.Clone()
        };

        score.LocalProperties = new LocalProperties().FromScore(score);

        EnrichScoreWithBeatmapStatus(score, ctx.Beatmap);

        ctx.UserPersonalBestScores = peers;

        foreach (var processor in _processors)
        {
            await DispatchProcessor(processor, ctx);
        }

        var refreshClaimLeaseResult = await TryRefreshClaimLease(task, ct);
        if (refreshClaimLeaseResult.IsFailure)
            throw new ApplicationException(refreshClaimLeaseResult.Error);

        return ctx;
    }

    private static void EnrichScoreWithBeatmapStatus(Score score, Beatmap? beatmap)
    {
        var newBeatmapStatus = beatmap?.Status;

        if (!newBeatmapStatus.HasValue || newBeatmapStatus == score.BeatmapStatus)
            return;

        score.BeatmapStatus = newBeatmapStatus.Value;
        score.IsScoreable = newBeatmapStatus.Value.IsScoreable();
        score.LocalProperties = score.LocalProperties.FromScore(score);
    }

    private async Task<UnitResult<string>> TryRefreshClaimLease(ScoreProcessingTask? task, CancellationToken ct)
    {
        if (task == null || string.IsNullOrWhiteSpace(task.ClaimToken))
            return UnitResult.Success<string>();

        var claimToken = task.ClaimToken;
        var leaseUntil = DateTime.UtcNow + Configuration.ScoreProcessingBatchLease;
        var rowsAffected = await _database.ScoreProcessingTasks.RefreshClaimLease(task.Id, claimToken, leaseUntil, ct);

        return rowsAffected == 0
            ? UnitResult.Failure($"Task {task.Id} claim lost; rolling back")
            : UnitResult.Success<string>();
    }

    private static async Task DispatchProcessor(IScoreEntityProcessor processor, ScoreCommitContext ctx)
    {
        switch (ctx.TaskType)
        {
            case ScoreTaskType.Submission:
                await processor.OnNewSubmission(ctx);
                break;
            case ScoreTaskType.Recalculation:
                await processor.OnRecalculation(ctx);
                break;
            case ScoreTaskType.Delete:
                await processor.OnDeletion(ctx);
                break;
            case ScoreTaskType.Restore:
                await processor.OnRestoration(ctx);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ctx.TaskType), ctx.TaskType, $"Unhandled task type: {ctx.TaskType}");
        }
    }
}