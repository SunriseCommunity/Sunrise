using System.Collections.Concurrent;
using System.Linq.Dynamic.Core;
using osu.Shared;
using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Seeders;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Processing.Scores.Processors;

[TraceExecution]
public class MedalScoreProcessor(DatabaseService database) : ScoreEntityProcessorBase
{
    public override int Priority => 300;

    protected override async Task OnNewSubmissionInternal(ScoreCommitContext ctx)
    {
        if (!ctx.Score.IsScoreable)
            return;

        if (ctx.Beatmap == null)
            throw new InvalidOperationException("Beatmap must be present in context to unlock medals.");

        ctx.UnlockedMedals = await UnlockAndGetNewMedals(ctx.Score, ctx.Beatmap, ctx.UserStats);
    }

    protected override async Task OnRecalculationInternal(ScoreCommitContext ctx)
    {
        if (!ctx.Score.IsScoreable)
            return;

        if (ctx.Beatmap == null)
            throw new InvalidOperationException("Beatmap must be present in context to unlock medals.");

        ctx.UnlockedMedals = await UnlockAndGetNewMedals(ctx.Score, ctx.Beatmap, ctx.UserStats);
    }

    protected override Task OnDeletionInternal(ScoreCommitContext ctx)
    {
        return Task.CompletedTask;
    }

    protected override Task OnRestorationInternal(ScoreCommitContext ctx)
    {
        return Task.CompletedTask;
    }

    private async Task<List<Medal>> UnlockAndGetNewMedals(Score score, Beatmap beatmap, UserStats userStats)
    {
        if (!score.IsPassed || !beatmap.Status.IsScoreable()) return [];

        var medals = await database.Medals.GetMedals(score.GameMode);
        var userMedals = await database.Users.Medals.GetUserMedals(userStats.UserId);

        var unlockedMedalIds = userMedals.Select(x => x.MedalId).ToHashSet();
        var medalsToCheck = medals.Where(m => !unlockedMedalIds.Contains(m.Id)).ToList();

        var eligibleMedals = new ConcurrentBag<Medal>();

        await Parallel.ForEachAsync(
            medalsToCheck,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            (medal, _) => EvaluateMedal(medal)
        );

        var newMedalsIds = eligibleMedals.Select(m => m.Id).ToList();
        var unlockMedalsResult = await database.Users.Medals.UnlockMedals(userStats.UserId, newMedalsIds);
        if (unlockMedalsResult.IsFailure)
            throw new ApplicationException("Failed to unlock medals: " + unlockMedalsResult.Error);

        return eligibleMedals.ToList();

        ValueTask EvaluateMedal(Medal medal)
        {
            var isConditionsAreMet = Evaluate(new MedalConditionContext
                {
                    user = userStats,
                    score = score,
                    beatmap = beatmap
                },
                medal.Condition);

            if (!isConditionsAreMet)
                return ValueTask.CompletedTask;

            var isScoreWithNoFailAndNotEligible
                = medal.Category != MedalCategory.ModIntroduction && score.Mods.HasFlag(Mods.NoFail);

            if (isScoreWithNoFailAndNotEligible)
                return ValueTask.CompletedTask;

            eligibleMedals.Add(medal);
            return ValueTask.CompletedTask;
        }
    }

    private static bool Evaluate<T>(T obj, string expression)
    {
        var objQueryable = new[]
        {
            obj
        }.AsQueryable();
        return objQueryable.Any(expression);
    }
}