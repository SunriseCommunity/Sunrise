using System.Linq.Dynamic.Core;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

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

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var medals = await database.GetMedals(score.GameMode);
        var userMedals = await database.GetUserMedals(userStats.UserId);

        foreach (var medal in medals)
        {
            if (userMedals.Any(x => x.MedalId == medal.Id)) continue;

            var isConditionsAreMet = Evaluate(
                new ConditionContext { user = userStats, score = score, beatmap = beatmap },
                medal.Condition);

            if (!isConditionsAreMet) continue;

            await database.UnlockMedal(userStats.UserId, medal.Id);
            newMedals.Add(medal.GetMedalString());
        }

        return string.Join("/", newMedals);
    }

    private static bool Evaluate<T>(T obj, string expression)
    {
        var objQueryable = new[] { obj }.AsQueryable();
        return objQueryable.Any(expression);
    }

    private static string GetMedalString(this Medal medal)
    {
        return $"{medal.Id}+{medal.Name}+{medal.Description}";
    }
}