using osu.Shared;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using Watson.ORM.Core;

namespace Sunrise.Server.Database.Models;

[Table("user_stats")]
public class UserStats
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Int, false)]
    public int UserId { get; set; }

    [Column(DataTypes.Int, false)]
    public GameMode GameMode { get; set; }

    [Column(DataTypes.Decimal, 100, 2, false)]
    public double Accuracy { get; set; }

    [Column(DataTypes.Double, 45, 2, false)]
    public long TotalScore { get; set; }

    [Column(DataTypes.Double, 45, 2, false)]
    public long RankedScore { get; set; }

    [Column(DataTypes.Int, false)]
    public int PlayCount { get; set; }

    [Column(DataTypes.Int, false)]
    public short PerformancePoints { get; set; }

    [Column(DataTypes.Int, false)]
    public int MaxCombo { get; set; }

    [Column(DataTypes.Int, false)]
    public int PlayTime { get; set; }

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
        IncreasePlayTime(timeElapsed);
        IncreasePlaycount();

        if (isFailed) return;

        if (score.Beatmap.Status < BeatmapStatus.Ranked) return;

        UpdateMaxCombo(score.MaxCombo);

        if (score.Beatmap.Status > BeatmapStatus.Approved) return;

        if (isNewScore || isBetterScore)
        {
            RankedScore += isNewScore ? score.TotalScore : score.TotalScore - prevScore!.TotalScore;

            PerformancePoints = (short)await Calculators.CalculateUserWeightedPerformance(score.UserId, score.GameMode, score);
            Accuracy = await Calculators.CalculateUserWeightedAccuracy(score.UserId, score.GameMode, score);
        }
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