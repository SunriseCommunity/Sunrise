using Sunrise.Shared.Objects;

namespace Sunrise.Shared.Tests.Objects;

public class ScoreProcessingProgressEstimatorTests
{
    [Fact]
    public void EstimatesFromWallClockProgressIncludingIdleTime()
    {
        var time = new TestTimeProvider();
        var estimator = new ScoreProcessingProgressEstimator(time);
        Assert.Null(estimator.EstimateSeconds(1000));

        time.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(540, estimator.EstimateSeconds(900));

        time.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(1080, estimator.EstimateSeconds(900));
    }

    [Fact]
    public void DoesNotInventAnEstimateBeforeProgressOrAfterAStall()
    {
        var time = new TestTimeProvider();
        var estimator = new ScoreProcessingProgressEstimator(time);
        Assert.Null(estimator.EstimateSeconds(1000));
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Null(estimator.EstimateSeconds(900));
        time.Advance(TimeSpan.FromSeconds(50));
        Assert.Null(estimator.EstimateSeconds(1100));
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.Null(estimator.EstimateSeconds(800));
    }

    [Fact]
    public void NewWorkStartsAFreshObservationWindow()
    {
        var time = new TestTimeProvider();
        var estimator = new ScoreProcessingProgressEstimator(time);
        estimator.EstimateSeconds(1000);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.NotNull(estimator.EstimateSeconds(900));
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Null(estimator.EstimateSeconds(950));
    }

    [Fact]
    public void CompletedQueueDoesNotContaminateTheNextRun()
    {
        var time = new TestTimeProvider();
        var estimator = new ScoreProcessingProgressEstimator(time);
        estimator.EstimateSeconds(1000);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Null(estimator.EstimateSeconds(0));
        Assert.Null(estimator.EstimateSeconds(200));
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }
}
