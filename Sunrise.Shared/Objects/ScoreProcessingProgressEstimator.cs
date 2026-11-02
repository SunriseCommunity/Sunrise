namespace Sunrise.Shared.Objects;

public sealed class ScoreProcessingProgressEstimator(TimeProvider timeProvider)
{
    private readonly Queue<(long Timestamp, long Outstanding)> _samples = new();
    private readonly object _sync = new();
    private long _lastOutstanding;

    public double? EstimateSeconds(long outstanding)
    {
        lock (_sync)
        {
            if (outstanding > _lastOutstanding)
                _samples.Clear();
            _lastOutstanding = outstanding;

            var now = timeProvider.GetTimestamp();
            while (_samples.Count > 0 && timeProvider.GetElapsedTime(_samples.Peek().Timestamp, now) > TimeSpan.FromMinutes(5))
                _samples.Dequeue();

            if (outstanding <= 0)
            {
                _samples.Clear();
                return null;
            }

            _samples.Enqueue((now, outstanding));
            var first = _samples.Peek();
            var elapsed = timeProvider.GetElapsedTime(first.Timestamp, now).TotalSeconds;
            var completed = first.Outstanding - outstanding;

            // Observe wall-clock progress, including idle time, before offering an estimate.
            return elapsed >= 30 && completed > 0 ? outstanding * elapsed / completed : null;
        }
    }
}
