using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Processing.Scores.Handlers;

public abstract class ScoreHandlerBase(
    DatabaseService database,
    ScoreCommitPipeline pipeline) : IScoreHandler
{

    protected DatabaseService Database { get; } = database;

    public async Task<UnitResult<ScoreProcessingError>> ExecuteAsync(ScoreProcessingTask task, CancellationToken ct)
    {
        var prepareResult = await PrepareAsync(task, ct);
        if (prepareResult.IsFailure)
            return UnitResult.Failure(prepareResult.Error);


        var commitResult = await CommitAsync(prepareResult.Value, task, ct);
        if (commitResult.IsFailure)
            return commitResult.Error;

        await OnCommitted(commitResult.Value, ct);
        return UnitResult.Success<ScoreProcessingError>();
    }

    internal virtual Task<Result<ScoreCommitContext, ScoreProcessingError>> PrepareAsync(
        ScoreProcessingTask task, CancellationToken ct)
    {
        throw new NotSupportedException($"{GetType().Name} does not implement PrepareAsync.");
    }

    protected async Task<Result<ScoreCommitContext, ScoreProcessingError>> CommitAsync(
        ScoreCommitContext ctx,
        ScoreProcessingTask? task,
        CancellationToken ct)
    {
        var commitResult = await pipeline.Commit(ctx, task, ct);

        if (commitResult.IsFailure)
        {
            var translated = TryTranslateTransactionFailure(commitResult.Error);
            if (translated.IsFailure)
                return translated.Error;

            Log.Warning("Failed to commit score state mutation, reason: {Reason}, ScoreId: {ScoreId}",
                commitResult.Error,
                ctx.Score.Id);

            return new ScoreProcessingError(
                ScoreProcessingErrorCode.TransactionFailed,
                $"Failed to commit score state mutation: {commitResult.Error}",
                ScoreProcessingDisposition.Retryable);
        }

        return ctx;
    }

    internal virtual Task OnCommitted(ScoreCommitContext ctx, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    [TraceExecution]
    protected async Task<Result<(User User, UserStats UserStats, UserGrades UserGrades), ScoreProcessingError>> LoadUserState(
        Score score, CancellationToken ct)
    {
        var user = score.User ?? await Database.Users.GetUser(
            score.UserId,
            options: new QueryOptions
            {
                QueryModifier = q => q.Cast<User>().Include(u => u.UserStats)
            },
            ct: ct);

        if (user == null)
        {
            Log.Warning("Couldn't find user while processing score {ScoreId}", score.Id);
            return new ScoreProcessingError(ScoreProcessingErrorCode.UserNotFound, "User not found")
                .ToResult<(User, UserStats, UserGrades)>();
        }

        var userStats = user.UserStats.FirstOrDefault(u => u.GameMode == score.GameMode)
                        ?? await Database.Users.Stats.GetUserStats(user.Id, score.GameMode, ct);

        if (userStats == null)
        {
            Log.Warning("User stats not found. ScoreId: {scoreId}", score.Id);
            return new ScoreProcessingError(ScoreProcessingErrorCode.UserStatsNotFound, "User stats not found")
                .ToResult<(User, UserStats, UserGrades)>();
        }

        var userGrades = await Database.Users.Grades.GetUserGrades(user.Id, userStats.GameMode, ct);

        if (userGrades == null)
        {
            Log.Warning("Couldn't find user grades while processing score {ScoreId}", score.Id);
            return new ScoreProcessingError(ScoreProcessingErrorCode.UserGradesNotFound, "User grades not found")
                .ToResult<(User, UserStats, UserGrades)>();
        }

        // TODO: Deprecate in favour of just tracking the get user ranks.
        var (currentRank, _) = await Database.Users.Stats.Ranks.GetUserRanks(user, userStats.GameMode, ct: ct);
        userStats.LocalProperties.Rank = currentRank;

        return (user, userStats, userGrades);
    }

    [TraceExecution]
    protected async Task<Result<(BeatmapSet BeatmapSet, Beatmap Beatmap), ScoreProcessingError>> ResolveBeatmap(
        BeatmapService beatmapService,
        BaseSession session,
        string beatmapHash,
        CancellationToken ct)
    {
        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapHash: beatmapHash, retryCount: 1, ct: ct);

        if (beatmapSetResult.IsFailure || beatmapSetResult.Value == null)
        {
            var disposition = beatmapSetResult.Error.Status == HttpStatusCode.NotFound
                ? ScoreProcessingDisposition.Permanent
                : ScoreProcessingDisposition.Retryable;

            return new ScoreProcessingError(ScoreProcessingErrorCode.BeatmapNotFound,
                    $"Failed to fetch beatmap set: {beatmapSetResult.Error.Message}",
                    disposition)
                .ToResult<(BeatmapSet, Beatmap)>();
        }

        var beatmapSet = beatmapSetResult.Value;
        var beatmap = beatmapSet?.Beatmaps?.FirstOrDefault(x => x.Checksum == beatmapHash);

        if (beatmapSet == null || beatmap == null)
            return new ScoreProcessingError(ScoreProcessingErrorCode.BeatmapNotFound, "BeatmapSet not found")
                .ToResult<(BeatmapSet, Beatmap)>();

        return (beatmapSet, beatmap);
    }

    protected static UnitResult<ScoreProcessingError> TryTranslateTransactionFailure(string errorMessage)
    {
        if (errorMessage.Contains("IX_score_ScoreHash", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("duplicate entry", StringComparison.OrdinalIgnoreCase)
            && errorMessage.Contains("ScoreHash", StringComparison.OrdinalIgnoreCase))
        {
            return new ScoreProcessingError(
                ScoreProcessingErrorCode.DuplicateScore,
                "Score with same hash already exists").ToUnit();
        }

        return UnitResult.Success<ScoreProcessingError>();
    }
}