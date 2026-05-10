using osu.Shared;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Objects;

public class SubmittedScore
{
    public required string PlayerUsername { get; init; }
    public required string ScoreHash { get; init; }
    public required string BeatmapHash { get; init; }
    public required long TotalScore { get; init; }
    public required int MaxCombo { get; init; }
    public required int Count300 { get; init; }
    public required int Count100 { get; init; }
    public required int Count50 { get; init; }
    public required int CountMiss { get; init; }
    public required int CountKatu { get; init; }
    public required int CountGeki { get; init; }
    public required bool Perfect { get; init; }
    public required Mods Mods { get; init; }
    public required string Grade { get; init; }
    public required bool IsPassed { get; init; }
    public required GameMode GameMode { get; set; }
    public required DateTime WhenPlayed { get; init; }
    public required string OsuVersion { get; init; }
    public required DateTime ClientTime { get; init; }
    public required double Accuracy { get; set; }
}