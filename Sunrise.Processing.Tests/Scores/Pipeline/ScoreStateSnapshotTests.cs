using Sunrise.Processing.Scores.Pipeline;
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
        score.LocalProperties = score.LocalProperties.FromScore(score);

        // Act
        var snapshot = ScoreStateSnapshot.Capture(score);

        // Assert
        Assert.Equal(score.SubmissionStatus, snapshot.SubmissionStatus);
        Assert.Equal(score.IsScoreable, snapshot.IsScoreable);
        Assert.Equal(score.IsPassed, snapshot.IsPassed);
        Assert.Equal(score.LocalProperties.IsRanked, snapshot.IsRanked);
    }
}