using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Dynamic.Core;
using osu.Shared;
using Sunrise.Shared.Attributes;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Seeders;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions.Beatmaps;
using Beatmap = Sunrise.Shared.Objects.Serializable.Beatmap;

namespace Sunrise.Server.Services;

public class MedalService(DatabaseService database)
{
    private static readonly ActivitySource ActivitySource = new("Sunrise.MedalService");

    [TraceExecution]
    public async Task<string> UnlockAndGetNewMedals(Score score, Beatmap beatmap, UserStats userStats)
    {
        List<string> newMedals = [];

        if (!score.IsPassed || !beatmap.Status.IsScoreable()) return string.Empty;

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

        foreach (var medal in eligibleMedals.OrderBy(m => m.Id))
        {
            await database.Users.Medals.UnlockMedal(userStats.UserId, medal.Id);
            newMedals.Add(GetMedalString(medal));
        }

        return string.Join("/", newMedals);

        ValueTask EvaluateMedal(Medal medal)
        {
            using var activity = ActivitySource.StartActivity($"Evaluating medal {medal.Id}");

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

    private static string GetMedalString(Medal medal)
    {
        return $"{medal.Id}+{medal.Name}+{medal.Description}";
    }
}