using System.Linq.Dynamic.Core;
using osu.Shared;
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
    public async Task<string> UnlockAndGetNewMedals(Score score, Beatmap beatmap, UserStats userStats)
    {
        List<string> newMedals = [];

        if (!score.IsPassed || !beatmap.Status.IsScoreable()) return string.Empty;

        var medals = await database.Medals.GetMedals(score.GameMode);
        var userMedals = await database.Users.Medals.GetUserMedals(userStats.UserId);

        foreach (var medal in medals)
        {
            if (userMedals.Any(x => x.MedalId == medal.Id)) continue;

            var isConditionsAreMet = Evaluate(
                new MedalConditionContext
                {
                    user = userStats,
                    score = score,
                    beatmap = beatmap
                },
                medal.Condition);

            if (!isConditionsAreMet) continue;

            // Note: Kind of hack to not give medals for passes with NoFail on non-ModIntroduction medals.
            if (medal.Category != MedalCategory.ModIntroduction && score.Mods.HasFlag(Mods.NoFail)) continue;

            await database.Users.Medals.UnlockMedal(userStats.UserId, medal.Id);
            newMedals.Add(GetMedalString(medal));
        }

        return string.Join("/", newMedals);
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