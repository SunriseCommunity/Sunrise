using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Extensions.Users;

public static class UserStatsExtensions
{
    // TODO: I personally don't like existance of this method. Ideally tests should have separate helper and production code shouldn't use this at all.
    public static void UpdateWithDbScore(this UserStats userStats, Score score)
    {
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        userStats.TotalScore += score.TotalScore;
        IncreaseTotalHits(userStats, score);
        userStats.PlayTime += score.TimeElapsed;
        userStats.PlayCount++;

        if (isFailed || !score.IsScoreable)
            return;

        userStats.MaxCombo = Math.Max(userStats.MaxCombo, score.MaxCombo);

        if (score.SubmissionStatus == SubmissionStatus.Best && score.BeatmapStatus.IsRanked())
            userStats.RankedScore += score.TotalScore;
    }

    private static void IncreaseTotalHits(UserStats userStats, Score score)
    {
        userStats.TotalHits += score.Count300 + score.Count100 + score.Count50;
        if ((GameMode)score.GameMode.ToVanillaGameMode() is GameMode.Taiko or GameMode.Mania)
            userStats.TotalHits += score.CountGeki + score.CountKatu;
    }
}
