using System.Linq.Dynamic.Core;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Types.Enums;
using Beatmap = Sunrise.Server.Objects.Serializable.Beatmap;

namespace Sunrise.Server.Managers;

public class ConditionContext
{
    public UserStats user { get; set; }
    public Score score { get; set; }
    public Beatmap beatmap { get; set; }
}

public static class MedalManager

{
    public static async Task<string> GetNewMedals(Score score, Beatmap beatmap, UserStats userStats)
    {
        List<string> newMedals = [];

        if (!score.IsPassed || beatmap.Status > BeatmapStatus.Ranked) return string.Empty;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var medals = await database.MedalService.GetMedals(score.GameMode);
        var userMedals = await database.UserService.Medals.GetUserMedals(userStats.UserId);

        foreach (var medal in medals)
        {
            if (userMedals.Any(x => x.MedalId == medal.Id)) continue;

            var isConditionsAreMet = Evaluate(
                new ConditionContext
                {
                    user = userStats,
                    score = score,
                    beatmap = beatmap
                },
                medal.Condition);

            if (!isConditionsAreMet) continue;

            // Note: Kind of a hack to not give medals for passes with NoFail on non-ModIntroduction medals.
            if (medal.Category != MedalCategory.ModIntroduction && score.Mods.HasFlag(Mods.NoFail)) continue;

            await database.UserService.Medals.UnlockMedal(userStats.UserId, medal.Id);
            newMedals.Add(medal.GetMedalString());
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

    private static string GetMedalString(this Medal medal)
    {
        return $"{medal.Id}+{medal.Name}+{medal.Description}";
    }
}