using Sunrise.Shared.Database.Models.Scores;

namespace Sunrise.Processing.Scores.Jobs;

internal static class ScoreProcessingDispatcher
{
    public static async Task<int> Drain(
        Func<int, IReadOnlyCollection<int>, CancellationToken, Task<List<ScoreProcessingTask>>> claim,
        Func<ScoreProcessingTask, CancellationToken, Task> process,
        int concurrency,
        CancellationToken ct)
    {
        var active = new List<(int Id, Task Work)>();
        var attempted = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var claimed = await claim(concurrency - active.Count, active.Select(t => t.Id).ToArray(), ct);
                foreach (var entry in claimed)
                    active.Add((entry.Id, process(entry, ct)));

                attempted += claimed.Count;
                if (active.Count == 0)
                    break;

                var completions = active.Select(t => t.Work);
                if (active.Count < concurrency)
                    completions = completions.Append(Task.Delay(TimeSpan.FromSeconds(1), ct));
                await Task.WhenAny(completions);
                foreach (var completed in active.Where(t => t.Work.IsCompleted).ToList())
                {
                    await completed.Work;
                    active.Remove(completed);
                }
            }
        }
        finally
        {
            // Keep queue ownership until every started task has stopped using its claim.
            await Task.WhenAll(active.Select(t => t.Work));
        }

        return attempted;
    }
}
