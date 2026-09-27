using Sunrise.Processing.Scores.Pipeline;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Services.Mock;
using Xunit;

namespace Sunrise.Processing.Tests.Scores.Pipeline;

public class ScoreStateSnapshotTests
{
    private readonly MockService _mocker = new();

    [Fact]
    public void TestCaptureWithRankedPassedScoreStoresCurrentState()
    {
        // Arrange
        var score = _mocker.Score.GetRandomScore();
        // Act
        var snapshot = ScoreStateSnapshot.Capture(score, score.BeatmapHashStatus!.Status);

        // Assert
        Assert.Equal(score.SubmissionStatus, snapshot.SubmissionStatus);
        Assert.Equal(score.BeatmapHashStatus!.Status.IsScoreable(), snapshot.IsScoreable);
        Assert.Equal(score.IsPassed, snapshot.IsPassed);
        Assert.Equal(score.BeatmapHashStatus!.Status.IsRanked(), snapshot.IsRanked);
    }
}
