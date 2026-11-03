using System.Net;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Calculations.Jobs;
using Sunrise.Processing.Scores.Jobs;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using Xunit;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Calculations.Jobs;

[Collection("Integration tests collection")]
public class CalculationRunJobTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture, true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestObservatoryCooldownStopsFurtherClaimsAndResumesWhenCleared()
    {
        EnvManager.Set("ScoreProcessing:MaxConcurrency", "1");
        await CreatePlayer(100, 100, 100);
        MockCalculator("3.2.0", 300);
        await RunJob();
        var redis = Scope.ServiceProvider.GetRequiredService<RedisRepository>();
        var requests = 0;
        App.MockHttpClient!.MockResponse(ApiType.CalculateScorePerformance, _ =>
        {
            requests++;
            redis.Set(RedisKey.ApiServerRateLimited(ApiServer.Observatory), true, TimeSpan.FromMinutes(1)).GetAwaiter().GetResult();
            return new ErrorMessage("Observatory rate limit reached", HttpStatusCode.TooManyRequests);
        });

        await ProcessQueue();

        var tasks = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().ToListAsync();
        Assert.Equal(1, requests);
        Assert.Equal(3, tasks.Count);
        Assert.Single(tasks, t => t.ErrorCode == ScoreProcessingErrorCode.RateLimited);
        Assert.All(tasks, t =>
        {
            Assert.Equal(ScoreProcessingStatus.Pending, t.Status);
            Assert.Equal(0, t.RetryCount);
            Assert.Null(t.ClaimToken);
            Assert.Null(t.LeaseExpiresAt);
        });

        await redis.Remove(RedisKey.ApiServerRateLimited(ApiServer.Observatory));
        await Database.DbContext.ScoreProcessingTasks.ExecuteUpdateAsync(s => s.SetProperty(t => t.NextRetryAt, (DateTime?)null));
        MockCalculator("3.2.0", 300);
        await ProcessQueue();
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
        await RunJob();
        Assert.NotNull((await Database.Calculations.GetLastRun())!.FinishedAt);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(25, true)]
    public async Task TestMissingFileRespectsGlobalRetryLimitAndDoesNotLoseScore(int retries, bool exhausted)
    {
        EnvManager.Set("ScoreProcessing:MaxRetries", "3");
        var player = await CreatePlayer(100);
        var originalVersion = player.Scores[0].CalculationVersionId;
        MockCalculator("3.2.0", 300);
        await RunJob();
        var run = (await Database.Calculations.GetLastRun())!;
        var task = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.RunId == run.Id);
        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == task.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RetryCount, retries));
        MockMissingOsuFile();

        await ProcessQueue();

        var failed = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal(retries + 1, failed.RetryCount);
        Assert.Equal(exhausted ? ScoreProcessingStatus.Failed : ScoreProcessingStatus.Pending, failed.Status);
        Assert.Equal(ScoreProcessingErrorCode.PpCalculationFailed, failed.ErrorCode);
        Assert.Equal("PP calculation failed: Osu file not found", failed.ErrorMessage);
        Assert.Null(failed.ClaimToken);
        Assert.Null(failed.LeaseExpiresAt);
        if (exhausted)
            Assert.Null(failed.NextRetryAt);
        else
            Assert.True(failed.NextRetryAt > DateTime.UtcNow);

        var unchanged = await ReloadScore(player.Scores[0]);
        Assert.Equal(100, unchanged.PerformancePoints);
        Assert.Equal(originalVersion, unchanged.CalculationVersionId);

        await RunJob();

        if (exhausted)
        {
            Assert.Null(await Database.Calculations.GetFrozenPhase());
            Assert.NotNull((await Database.Calculations.GetLastRun())!.FinishedAt);
            Assert.Equal(1, (await Database.Calculations.CountRunTasks(run))[ScoreProcessingStatus.Failed]);
            Assert.Equal(originalVersion, (await ReloadScore(player.Scores[0])).CalculationVersionId);
        }
        else
        {
            Assert.Equal(CalculationRunPhase.Scores, await Database.Calculations.GetFrozenPhase());
        }
    }

    [Fact]
    public async Task TestExhaustedGlobalTaskCanBeRequeuedWhenFileBecomesAvailable()
    {
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 300);
        await RunJob();
        var task = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.RunId != null);
        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == task.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RetryCount, Configuration.ScoreProcessingMaxRetries * 2 - 1));
        MockMissingOsuFile();
        await ProcessQueue();
        await RunJob();

        Assert.True((await Database.ScoreProcessingTasks.TryRequeueFailedTask(task.Id)).IsSuccess);
        MockCalculator("3.2.0", 300);
        await ProcessQueue();

        var recovered = await ReloadScore(player.Scores[0]);
        Assert.Equal(300, recovered.PerformancePoints);
        Assert.Equal(await Database.Calculations.GetOrCreateVersionId("3.2.0"), recovered.CalculationVersionId);
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync(t => t.Id == task.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestRateLimitingDefersGlobalTaskWithoutConsumingRetries(bool metadataRateLimited)
    {
        var retries = Configuration.ScoreProcessingMaxRetries * 2;
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 300);
        await RunJob();
        var task = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.RunId != null);
        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == task.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RetryCount, retries));

        var requests = 0;
        if (metadataRateLimited)
            await Scope.ServiceProvider.GetRequiredService<RedisRepository>().Remove(RedisKey.BeatmapSetIdByHash(player.Scores[0].BeatmapHash));
        App.MockHttpClient!.MockResponse(metadataRateLimited ? ApiType.BeatmapSetDataByHash : ApiType.CalculateScorePerformance, _ =>
        {
            requests++;
            return new ErrorMessage("Observatory rate limit reached", HttpStatusCode.TooManyRequests);
        });

        await ProcessQueue();

        var deferred = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal(ScoreProcessingStatus.Pending, deferred.Status);
        Assert.Equal(retries, deferred.RetryCount);
        Assert.Equal(ScoreProcessingErrorCode.RateLimited, deferred.ErrorCode);
        Assert.True(deferred.NextRetryAt > DateTime.UtcNow);
        Assert.Null(deferred.ClaimToken);
        Assert.Null(deferred.LeaseExpiresAt);
        Assert.Equal(1, requests);
        Assert.Equal(100, (await ReloadScore(player.Scores[0])).PerformancePoints);

        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == task.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.NextRetryAt, DateTime.UtcNow.AddMinutes(-1)));
        MockCalculator("3.2.0", 300);
        await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(player.Scores[0]);
        await ProcessQueue();
        Assert.Equal(300, (await ReloadScore(player.Scores[0])).PerformancePoints);
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync(t => t.Id == task.Id));
    }

    private void MockMissingOsuFile()
    {
        App.MockHttpClient!.MockResponse(ApiType.CalculateScorePerformance, _ => new ErrorMessage
        {
            Message = "Osu file not found",
            Status = HttpStatusCode.NotFound
        });
    }

    [Fact]
    public async Task TestGlobalScorePhaseDefersWeightedQueriesUntilFinalStatsPhase()
    {
        var player = await CreatePlayer(100, 150);
        MockCalculator("3.2.0", 300);
        await RunJob();

        using (var scoreQueries = new SqlCommandCapture())
        {
            await ProcessQueue();
            Assert.DoesNotContain(scoreQueries.Commands, sql => sql.Contains("ROW_NUMBER()"));
        }

        Assert.Equal(300, (await ReloadScore(player.Scores[0])).PerformancePoints);
        Assert.Equal(player.Stats.PerformancePoints, (await ReloadStats(player)).PerformancePoints);

        using (var finalQueries = new SqlCommandCapture())
        {
            await RunJob();
            Assert.Contains(finalQueries.Commands, sql => sql.Contains("ROW_NUMBER()"));
        }

        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.True((await ReloadStats(player)).PerformancePoints > player.Stats.PerformancePoints);
    }

    [Fact]
    public async Task TestRunFreezesUserStatsUntilFinished()
    {
        // Arrange
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 200);

        // Act
        await RunJob();
        await ProcessQueue();

        // Assert
        Assert.Equal(200, (await ReloadScore(player.Scores[0])).PerformancePoints);
        Assert.Equal(player.Stats.PerformancePoints, (await ReloadStats(player)).PerformancePoints);
        Assert.NotNull(await Database.Calculations.GetFrozenPhase());

        await RunJob();

        var recalculatedScore = await ReloadScore(player.Scores[0]);
        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.Equal(await Database.Calculations.GetOrCreateVersionId("3.2.0"), recalculatedScore.CalculationVersionId);
        Assert.Equal(PerformanceCalculator.CalculateUserWeightedPerformance([recalculatedScore]), (await ReloadStats(player)).PerformancePoints);
    }

    [Fact]
    public async Task TestLeaderboardKeepsOldRanksUntilRunFinishes()
    {
        // Arrange
        var climber = await CreatePlayer(50, 50);
        var leader = await CreatePlayer(200);
        Assert.True(await GlobalRank(leader) < await GlobalRank(climber));

        MockCalculator("3.2.0", 300);

        // Act
        await RunJob();
        await ProcessQueue();

        // Assert
        Assert.Equal(300, (await ReloadScore(climber.Scores[0])).PerformancePoints);
        Assert.True(await GlobalRank(leader) < await GlobalRank(climber));

        await RunJob();

        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.True(await GlobalRank(climber) < await GlobalRank(leader));
        Assert.True((await ReloadStats(climber)).PerformancePoints > (await ReloadStats(leader)).PerformancePoints);
    }

    [Fact]
    public async Task TestStatusChangeDuringRunIsAppliedAndRunStillFinishes()
    {
        // Arrange
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 300);
        await RunJob();

        // Act
        await Database.Calculations.ApplyBeatmapHashStatus(player.Scores[0].BeatmapHash, player.Scores[0].BeatmapId, BeatmapStatus.Pending);
        await ProcessQueue();
        await RunJob();

        // Assert
        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.Equal(0, (await ReloadStats(player)).PerformancePoints);
        Assert.Equal(SubmissionStatus.Submitted, (await ReloadScore(player.Scores[0])).SubmissionStatus);
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
    }

    [Fact]
    public async Task TestRunFinishesWhenCalculatorReportsDifferentVersionString()
    {
        // Arrange
        await CreatePlayer(100);
        App.MockHttpClient!.MockResponse(ApiType.GetCalculatorVersion, _ => new CalculatorVersionResponse { Rosu = "3.2.0" });
        App.MockHttpClient.MockPerformanceCalculation(performancePoints: 300, rosuVersion: "3.2.0-build.7");

        // Act
        await RunJob();
        await ProcessQueue();
        await RunJob();

        // Assert
        Assert.Null(await Database.Calculations.GetFrozenPhase());
    }

    [Fact]
    public async Task TestRunWaitsForOutdatedScoreBusyWithAnotherTask()
    {
        // Arrange
        var player = await CreatePlayer(100);
        var otherTask = new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Recalculation,
            ScoreId = player.Scores[0].Id,
            Status = ScoreProcessingStatus.Processing,
            ClaimToken = "other-worker",
            LeaseExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(otherTask);
        MockCalculator("3.2.0", 300);

        // Act
        await RunJob();
        await RunJob();
        var phaseWhileBusy = await Database.Calculations.GetFrozenPhase();

        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == otherTask.Id).ExecuteDeleteAsync();
        await RunJob();
        await ProcessQueue();
        await RunJob();

        // Assert
        Assert.Equal(CalculationRunPhase.Scores, phaseWhileBusy);
        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.Equal(await Database.Calculations.GetOrCreateVersionId("3.2.0"), (await ReloadScore(player.Scores[0])).CalculationVersionId);
    }

    [Fact]
    public async Task TestSimultaneousStartsLeaveOneUnfinishedRun()
    {
        // Arrange
        var targetVersionId = (await Database.Calculations.GetOrCreateVersionId("3.2.0"))!.Value;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            // Act
            await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                try
                {
                    using var scope = App.Server.Services.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<DatabaseService>().Calculations.StartRun(targetVersionId, isForced: true);
                }
                catch (Exception ex) when (ex.ToString().Contains("Deadlock"))
                {
                    using var retryScope = App.Server.Services.CreateScope();
                    await retryScope.ServiceProvider.GetRequiredService<DatabaseService>().Calculations.StartRun(targetVersionId, isForced: true);
                }
            }));

            // Assert
            Assert.Equal(1, await Database.DbContext.CalculationRuns.NotCacheable().CountAsync(r => r.FinishedAt == null));
        }
    }

    [Fact]
    public async Task TestRunFinishesWhenItsTaskForAScoreWasCancelled()
    {
        // Arrange
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 300);
        await RunJob();
        var runTask = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.RunId != null);
        Assert.True((await Database.ScoreProcessingTasks.CancelTask(runTask.Id)).IsSuccess);

        // Act
        await RunJob();

        // Assert
        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.NotEqual(await Database.Calculations.GetOrCreateVersionId("3.2.0"), (await ReloadScore(player.Scores[0])).CalculationVersionId);
    }

    [Fact]
    public async Task TestFinishedRunRecordsNewBestRanks()
    {
        // Arrange
        var leader = await CreatePlayer(200);
        var climber = await CreatePlayer(50, 50);
        Assert.NotEqual(1, (await ReloadStats(climber)).BestGlobalRank);

        MockCalculator("3.2.0", 300);

        // Act
        await RunJob();
        await ProcessQueue();
        await RunJob();

        // Assert
        Assert.Equal(1, await GlobalRank(climber));
        Assert.Equal(1, (await ReloadStats(climber)).BestGlobalRank);
        Assert.Equal(1, (await ReloadStats(leader)).BestGlobalRank);
    }

    [Fact]
    public async Task TestFreezeBlocksLeaderboardAndPpUpdatesFromOtherScoreProcessing()
    {
        // Arrange
        var climber = await CreatePlayer(50, 50);
        var leader = await CreatePlayer(200);
        var targetVersionId = (await Database.Calculations.GetOrCreateVersionId("3.2.0"))!.Value;
        await Database.Calculations.StartRun(targetVersionId);

        MockCalculator("3.2.0", 1000);

        // Act
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([climber.Scores[0].Id], ScoreTaskType.Recalculation, ScoreProcessingPriority.High);
        await ProcessQueue();

        // Assert
        Assert.Equal(1000, (await ReloadScore(climber.Scores[0])).PerformancePoints);
        Assert.Equal(climber.Stats.PerformancePoints, (await ReloadStats(climber)).PerformancePoints);
        Assert.True(await GlobalRank(leader) < await GlobalRank(climber));

        await RunJob();
        await ProcessQueue();
        await RunJob();

        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.True(await GlobalRank(climber) < await GlobalRank(leader));
    }

    [Fact]
    public async Task TestUserStatsPhaseUpdatesPpButNotLeaderboard()
    {
        // Arrange
        var climber = await CreatePlayer(50, 50);
        var leader = await CreatePlayer(200);
        var targetVersionId = (await Database.Calculations.GetOrCreateVersionId("3.2.0"))!.Value;
        var run = await Database.Calculations.StartRun(targetVersionId);
        await Database.DbContext.CalculationRuns.Where(r => r.Id == run.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Phase, CalculationRunPhase.UserStats));

        MockCalculator("3.2.0", 1000);

        // Act
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([climber.Scores[0].Id], ScoreTaskType.Recalculation, ScoreProcessingPriority.High);
        await ProcessQueue();

        // Assert
        Assert.True((await ReloadStats(climber)).PerformancePoints > (await ReloadStats(leader)).PerformancePoints);
        Assert.True(await GlobalRank(leader) < await GlobalRank(climber));
    }

    [Fact]
    public async Task TestUpdateUserStatsOutsideScoreProcessingRespectsFreeze()
    {
        // Arrange
        var climber = await CreatePlayer(50, 50);
        var leader = await CreatePlayer(200);
        var targetVersionId = (await Database.Calculations.GetOrCreateVersionId("3.2.0"))!.Value;
        var run = await Database.Calculations.StartRun(targetVersionId);

        // Act
        var frozenStats = await ReloadTrackedStats(climber);
        frozenStats.PerformancePoints = 10_000;
        await Database.Users.Stats.UpdateUserStats(frozenStats, climber.User);

        var ppDuringScoresPhase = (await ReloadStats(climber)).PerformancePoints;
        var climberRankDuringScoresPhase = await GlobalRank(climber);

        await Database.DbContext.CalculationRuns.Where(r => r.Id == run.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Phase, CalculationRunPhase.UserStats));

        var userStatsPhaseStats = await ReloadTrackedStats(climber);
        userStatsPhaseStats.PerformancePoints = 10_000;
        await Database.Users.Stats.UpdateUserStats(userStatsPhaseStats, climber.User);

        // Assert
        Assert.Equal(climber.Stats.PerformancePoints, ppDuringScoresPhase);
        Assert.True(await GlobalRank(leader) < climberRankDuringScoresPhase);

        Assert.Equal(10_000, (await ReloadStats(climber)).PerformancePoints);
        Assert.True(await GlobalRank(leader) < await GlobalRank(climber));
    }

    [Fact]
    public async Task TestVersionChangeSupersedesRunAndDeletesItsPendingTasks()
    {
        // Arrange
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 200);
        await RunJob();
        var firstRun = (await Database.Calculations.GetLastRun())!;

        // Act
        MockCalculator("2.0.0", 50);
        await RunJob();

        // Assert
        var supersededRun = await Database.DbContext.CalculationRuns.NotCacheable().SingleAsync(r => r.Id == firstRun.Id);
        var newRun = (await Database.Calculations.GetLastRun())!;

        Assert.True(supersededRun.IsSuperseded);
        Assert.NotNull(supersededRun.FinishedAt);
        Assert.Null(newRun.FinishedAt);
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync(t => t.RunId == firstRun.Id));
        Assert.True(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync(t => t.RunId == newRun.Id && t.ScoreId == player.Scores[0].Id));
    }

    [Fact]
    public async Task TestForcedRunRecalculatesScoresAlreadyOnTargetVersion()
    {
        // Arrange
        var player = await CreatePlayer(100);
        MockCalculator("3.2.0", 200);
        await RunJob();
        await ProcessQueue();
        await RunJob();

        var versionId = (await Database.Calculations.GetOrCreateVersionId("3.2.0"))!.Value;
        MockCalculator("3.2.0", 250);

        // Act
        await Database.Calculations.StartRun(versionId, isForced: true);
        await RunJob();
        await ProcessQueue();
        await RunJob();

        // Assert
        Assert.Equal(250, (await ReloadScore(player.Scores[0])).PerformancePoints);
        Assert.Null(await Database.Calculations.GetFrozenPhase());
    }

    [Fact]
    public async Task TestStopRunLiftsFreezeAndLeaderboardUpdatesAgain()
    {
        // Arrange
        var climber = await CreatePlayer(50, 50);
        var leader = await CreatePlayer(200);
        MockCalculator("3.2.0", 1000);
        await RunJob();
        var run = (await Database.Calculations.GetLastRun())!;

        // Act
        await Database.Calculations.StopRun(run);

        // Assert
        Assert.Null(await Database.Calculations.GetFrozenPhase());
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync(t => t.RunId == run.Id));

        await RunJob();
        Assert.Equal(run.Id, (await Database.Calculations.GetLastRun())!.Id);

        await Database.ScoreProcessingTasks.BulkAddScoreTasks([climber.Scores[0].Id], ScoreTaskType.Recalculation, ScoreProcessingPriority.High);
        await ProcessQueue();

        Assert.True(await GlobalRank(climber) < await GlobalRank(leader));
    }

    private record Player(User User, List<Score> Scores, UserStats Stats);

    private async Task<Player> CreatePlayer(params double[] performancePoints)
    {
        var user = await CreateTestUser();
        var scores = new List<Score>();

        foreach (var pp in performancePoints)
        {
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithUserData(user);
            score.GameMode = GameMode.Standard;
            score.Mods = Mods.None;
            score.PerformancePoints = pp;
            score = await CreateTestScore(score);
            await _mocker.Beatmap.MockRankedBeatmapWithSetForScore(score);
            scores.Add(score);
        }

        var stats = (await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard))!;
        (stats.PerformancePoints, stats.Accuracy) = await Scope.ServiceProvider.GetRequiredService<CalculatorService>().CalculateUserWeightedStats(user, GameMode.Standard);
        await Database.Users.Stats.UpdateUserStats(stats, user);

        return new Player(user, scores, stats);
    }

    private void MockCalculator(string rosuVersion, double performancePoints)
    {
        App.MockHttpClient!.MockResponse(ApiType.GetCalculatorVersion, _ => new CalculatorVersionResponse { Rosu = rosuVersion });
        App.MockHttpClient.MockPerformanceCalculation(performancePoints: performancePoints, rosuVersion: rosuVersion);
    }

    private async Task RunJob()
    {
        await Scope.ServiceProvider.GetRequiredService<CalculationRunJob>().Run(CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();
    }

    private async Task ProcessQueue()
    {
        await Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>().ProcessQueue(CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();
    }

    private async Task<long> GlobalRank(Player player)
    {
        return (await Database.Users.Stats.Ranks.GetUserRanks(player.User, GameMode.Standard, false)).globalRank;
    }

    private async Task<Score> ReloadScore(Score score)
    {
        Database.DbContext.ChangeTracker.Clear();
        return await Database.DbContext.Scores.NotCacheable().AsNoTracking().SingleAsync(s => s.Id == score.Id);
    }

    private async Task<UserStats> ReloadTrackedStats(Player player)
    {
        Database.DbContext.ChangeTracker.Clear();
        return await Database.DbContext.UserStats.NotCacheable().SingleAsync(s => s.Id == player.Stats.Id);
    }

    private async Task<UserStats> ReloadStats(Player player)
    {
        Database.DbContext.ChangeTracker.Clear();
        return await Database.DbContext.UserStats.NotCacheable().AsNoTracking().SingleAsync(s => s.Id == player.Stats.Id);
    }
}
