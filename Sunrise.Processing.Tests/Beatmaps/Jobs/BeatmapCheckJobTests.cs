using Sunrise.Shared.Extensions.Beatmaps;
using System.Net;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Beatmaps.Jobs;
using Sunrise.Processing.Scores.Jobs;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Xunit;
using Mods = osu.Shared.Mods;

namespace Sunrise.Processing.Tests.Beatmaps.Jobs;

[Collection("Integration tests collection")]
public class BeatmapCheckJobTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture, true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestHashIsMarkedNotSubmittedAfterSecondMissAndScoresAreDeranked()
    {
        // Arrange
        var score = await CreateUserScore();
        var user = (await Database.Users.GetUser(score.UserId))!;

        var stats = (await Database.Users.Stats.GetUserStats(user.Id, score.GameMode))!;
        (stats.PerformancePoints, stats.Accuracy) = await Scope.ServiceProvider.GetRequiredService<CalculatorService>().CalculateUserWeightedStats(user, score.GameMode);
        await Database.Users.Stats.UpdateUserStats(stats, user);
        Assert.True(stats.PerformancePoints > 0);

        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => new ErrorMessage { Message = "Not found", Status = HttpStatusCode.NotFound });

        // Act
        await RunChecks();
        var afterFirstMiss = await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash);

        await RunChecks();
        await ProcessQueue();

        // Assert
        Assert.Equal(1, afterFirstMiss!.MissCount);
        Assert.Equal(BeatmapStatus.Ranked, afterFirstMiss.Status);

        Assert.Equal(BeatmapStatus.NotSubmitted, (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!.Status);
        Assert.False((await ReloadScore(score)).BeatmapHashStatus!.Status.IsScoreable());
        Assert.True((await Database.DbContext.UserStats.NotCacheable().AsNoTracking().SingleAsync(s => s.Id == stats.Id)).PerformancePoints < stats.PerformancePoints);
    }

    [Fact]
    public async Task TestStatusChangeUpdatesHashRowAndQueuesRecalculation()
    {
        // Arrange
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        var score = await CreateUserScore();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        beatmap.StatusString = "loved";
        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => beatmapSet);

        // Act
        await RunChecks();

        // Assert
        Assert.Equal(BeatmapStatus.Loved, (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!.Status);
        Assert.Equal(BeatmapStatus.Loved, (await ReloadScore(score)).BeatmapHashStatus!.Status);
        Assert.True(await Database.DbContext.ScoreProcessingTasks.NotCacheable()
            .AnyAsync(t => t.ScoreId == score.Id && t.TaskType == ScoreTaskType.Recalculation && t.Status == ScoreProcessingStatus.Pending));
    }

    [Fact]
    public async Task TestDeadHashFoundAgainIsRestoredAndScoresCountAgain()
    {
        // Arrange
        var score = await CreateUserScore();
        await Database.DbContext.BeatmapHashStatuses.Where(h => h.BeatmapHash == score.BeatmapHash)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, BeatmapStatus.NotSubmitted).SetProperty(h => h.MissCount, 2));

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        beatmap.StatusString = "ranked";
        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => beatmapSet);
        App.MockHttpClient.MockPerformanceCalculation(performancePoints: 150);

        // Act
        await RunChecks();
        await ProcessQueue();

        // Assert
        var beatmapHashStatus = await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash);
        Assert.Equal(BeatmapStatus.Ranked, beatmapHashStatus!.Status);
        Assert.Equal(0, beatmapHashStatus.MissCount);
        Assert.True((await ReloadScore(score)).BeatmapHashStatus!.Status.IsScoreable());
    }

    [Fact]
    public async Task TestBatOverrideIsAppliedToHashOnNextCheck()
    {
        // Arrange
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        var score = await CreateUserScore();

        var batUser = await CreateTestUser();
        batUser.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(batUser);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        beatmap.StatusString = "ranked";
        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => beatmapSet);

        Assert.False(await Database.Calculations.GetDueBeatmapChecks().AnyAsync(h => h.BeatmapHash == score.BeatmapHash));

        // Act
        var changeResult = await Scope.ServiceProvider.GetRequiredService<BeatmapService>().ChangeBeatmapCustomStatus(batUser, beatmap, BeatmapStatusWeb.Loved, null);
        var isDueAfterOverride = await Database.Calculations.GetDueBeatmapChecks().AnyAsync(h => h.BeatmapHash == score.BeatmapHash);

        await Scope.ServiceProvider.GetRequiredService<BeatmapCheckJob>().Run(CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();

        // Assert
        Assert.True(changeResult.IsSuccess, changeResult.IsFailure ? changeResult.Error : null);
        Assert.True(isDueAfterOverride);
        Assert.Equal(BeatmapStatus.Loved, (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!.Status);
        Assert.True(await Database.DbContext.ScoreProcessingTasks.NotCacheable()
            .AnyAsync(t => t.ScoreId == score.Id && t.TaskType == ScoreTaskType.Recalculation && t.Status == ScoreProcessingStatus.Pending));
    }

    [Fact]
    public async Task TestChecksAreCappedPerTick()
    {
        // Arrange
        for (var i = 0; i < 60; i++)
        {
            Database.DbContext.BeatmapHashStatuses.Add(new BeatmapHashStatus
            {
                BeatmapHash = Guid.NewGuid().ToString("N"),
                BeatmapId = i,
                CheckedAt = DateTime.UtcNow.AddDays(-2)
            });
        }

        await Database.DbContext.SaveChangesAsync();
        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => new ErrorMessage { Message = "Not found", Status = HttpStatusCode.NotFound });

        // Act
        await Scope.ServiceProvider.GetRequiredService<BeatmapCheckJob>().Run(CancellationToken.None);

        // Assert
        Assert.Equal(10, await Database.Calculations.GetDueBeatmapChecks().CountAsync());
    }

    private async Task<Score> CreateUserScore()
    {
        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.Mods = Mods.None;
        score.GameMode = GameMode.Standard;
        score.PerformancePoints = 100;
        return await CreateTestScore(score);
    }

    private async Task RunChecks()
    {
        await Database.DbContext.BeatmapHashStatuses.ExecuteUpdateAsync(s => s.SetProperty(h => h.CheckedAt, DateTime.UtcNow.AddDays(-2)));
        await Scope.ServiceProvider.GetRequiredService<BeatmapCheckJob>().Run(CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();
    }

    private async Task ProcessQueue()
    {
        await Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>().ProcessQueue(CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();
    }

    private async Task<Score> ReloadScore(Score score)
    {
        Database.DbContext.ChangeTracker.Clear();
        return await Database.DbContext.Scores.NotCacheable().AsNoTracking().SingleAsync(s => s.Id == score.Id);
    }
}
