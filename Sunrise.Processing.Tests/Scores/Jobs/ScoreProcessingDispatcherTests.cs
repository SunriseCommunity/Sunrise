using Sunrise.Processing.Scores.Jobs;
using Sunrise.Shared.Database.Models.Scores;
using Xunit;

namespace Sunrise.Processing.Tests.Scores.Jobs;

public class ScoreProcessingDispatcherTests
{
    [Fact]
    public async Task RefillsCompletedSlotWhileAnotherTaskIsStillRunning()
    {
        var queue = new Queue<ScoreProcessingTask>(Enumerable.Range(1, 3).Select(id => new ScoreProcessingTask { Id = id }));
        var releaseSlowTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var maxRunning = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var draining = ScoreProcessingDispatcher.Drain(Claim, Process, 2, timeout.Token);
        try
        {
            await thirdStarted.Task.WaitAsync(timeout.Token);
            Assert.False(releaseSlowTask.Task.IsCompleted);
        }
        finally
        {
            releaseSlowTask.TrySetResult();
            await draining;
        }

        Assert.Equal(3, await draining);
        Assert.Equal(2, maxRunning);

        Task<List<ScoreProcessingTask>> Claim(int available, IReadOnlyCollection<int> activeIds, CancellationToken ct)
        {
            if (queue.Count == 1)
                Assert.Contains(1, activeIds);
            var claimed = new List<ScoreProcessingTask>();
            while (available-- > 0 && queue.TryDequeue(out var entry))
                claimed.Add(entry);
            return Task.FromResult(claimed);
        }

        async Task Process(ScoreProcessingTask task, CancellationToken ct)
        {
            maxRunning = Math.Max(maxRunning, Interlocked.Increment(ref running));
            try
            {
                if (task.Id == 1)
                    await releaseSlowTask.Task.WaitAsync(ct);
                if (task.Id == 3)
                    thirdStarted.TrySetResult();
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }
    }

    [Fact]
    public async Task ChecksForNewWorkWhileAnOlderTaskOccupiesOnlyOneSlot()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newWorkStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claims = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var draining = ScoreProcessingDispatcher.Drain(
            (_, _, _) => Task.FromResult(++claims switch
            {
                1 => new List<ScoreProcessingTask> { new() { Id = 1 } },
                3 => new List<ScoreProcessingTask> { new() { Id = 2 } },
                _ => []
            }),
            (task, _) =>
            {
                if (task.Id == 1)
                    return release.Task;
                newWorkStarted.TrySetResult();
                return Task.CompletedTask;
            },
            2, timeout.Token);

        try
        {
            await newWorkStarted.Task.WaitAsync(timeout.Token);
            Assert.False(release.Task.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await draining;
        }
    }

    [Fact]
    public async Task ReturnsOnlyAfterInFlightWorkCompletes()
    {
        var claimed = false;
        var completed = false;
        var attempted = await ScoreProcessingDispatcher.Drain(
            (_, _, _) => Task.FromResult(claimed ? new List<ScoreProcessingTask>() : Claim()),
            async (_, ct) =>
            {
                await Task.Delay(80, ct);
                completed = true;
            },
            1, CancellationToken.None);

        Assert.Equal(1, attempted);
        Assert.True(completed);

        List<ScoreProcessingTask> Claim()
        {
            claimed = true;
            return [new ScoreProcessingTask()];
        }
    }

    [Fact]
    public async Task ClaimFailureStillWaitsForAlreadyStartedWork()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claimFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var claims = 0;
        var draining = ScoreProcessingDispatcher.Drain(
            (_, _, _) =>
            {
                if (++claims == 1)
                    return Task.FromResult(new List<ScoreProcessingTask> { new() { Id = 1 }, new() { Id = 2 } });
                claimFailed.TrySetResult();
                throw new InvalidOperationException("claim failed");
            },
            (task, _) => task.Id == 1 ? release.Task : Task.CompletedTask,
            2, CancellationToken.None);

        try
        {
            await claimFailed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(draining.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => draining);
    }
}
