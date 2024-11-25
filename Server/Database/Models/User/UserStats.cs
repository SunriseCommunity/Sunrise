using osu.Shared;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models.User;

[Table("user_stats")]
public class UserStats
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public GameMode GameMode { get; set; }

    [Column(DataTypes.Double, 3, 2, false)]
    public double Accuracy { get; set; }

    [Column(DataTypes.Int, false)]
    public long TotalScore { get; set; }

    [Column(DataTypes.Int, false)]
    public long RankedScore { get; set; }

    [Column(DataTypes.Int, false)]
    public int PlayCount { get; set; }

    [Column(DataTypes.Double, int.MaxValue, int.MaxValue, false)]
    public double PerformancePoints { get; set; }

    [Column(DataTypes.Int, false)]
    public int MaxCombo { get; set; }

    [Column(DataTypes.Int, false)]
    public int PlayTime { get; set; }

    [Column(DataTypes.Int, false)]
    public int TotalHits { get; set; }

    [Column(DataTypes.Long)]
    public long? BestGlobalRank { get; set; }

    [Column(DataTypes.DateTime)]
    public DateTime? BestGlobalRankDate { get; set; }

    [Column(DataTypes.Long)]
    public long? BestCountryRank { get; set; }

    [Column(DataTypes.DateTime)]
    public DateTime? BestCountryRankDate { get; set; }

    // TODO: Remove local to extend of class
    // Local property
    public long? Rank { get; set; }

    public UserStats Clone()
    {
        return (UserStats)MemberwiseClone();
    }

    public async Task UpdateWithScore(Score score, Score? prevScore, int timeElapsed)
    {
        var isNewScore = prevScore == null;
        var isBetterScore = !isNewScore && score.TotalScore > prevScore!.TotalScore;
        var isFailed = !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);

        IncreaseTotalScore(score.TotalScore);
        IncreaseTotalHits(score);
        IncreasePlayTime(timeElapsed);
        IncreasePlaycount();

        if (isFailed || !score.IsRanked)
            return;

        UpdateMaxCombo(score.MaxCombo);

        if ((isNewScore || isBetterScore) && score.BeatmapStatus != BeatmapStatus.Ranked && score.BeatmapStatus != BeatmapStatus.Approved)
        {
            RankedScore += isNewScore ? score.TotalScore : score.TotalScore - prevScore!.TotalScore;

            PerformancePoints =
                await Calculators.CalculateUserWeightedPerformance(score.UserId, score.GameMode, score);
            Accuracy = await Calculators.CalculateUserWeightedAccuracy(score.UserId, score.GameMode, score);
        }
    }

    private void IncreaseTotalHits(Score newScore)
    {
        TotalHits += newScore.Count300 + newScore.Count100 + newScore.Count50;
        if (GameMode is GameMode.Taiko or GameMode.Mania)
            TotalHits += newScore.CountGeki + newScore.CountKatu;
    }

    private void UpdateMaxCombo(int combo)
    {
        MaxCombo = Math.Max(MaxCombo, combo);
    }

    private void IncreasePlayTime(int time)
    {
        PlayTime += time;
    }

    private void IncreaseTotalScore(long score)
    {
        TotalScore += score;
    }

    private void IncreasePlaycount()
    {
        PlayCount++;
    }
}