using Sunrise.Shared.Extensions.Beatmaps;
using System.Data;
using System.Net;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Beatmaps.Jobs;
using Sunrise.Processing.Scores.Handlers;
using Sunrise.Processing.Scores.Jobs;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Services;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils.Processing;
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
    public async Task TestStatusChangeUpdatesHashRowAndQueuesStatusChangeBelowSubmissions()
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
            .AnyAsync(t => t.ScoreId == score.Id && t.TaskType == ScoreTaskType.BeatmapStatusChange && t.Priority == (int)ScoreProcessingPriority.Medium));
    }

    [Fact]
    public async Task TestStatusChangesMoveRankedScoreMaxComboAndGradesOncePerUser()
    {
        // Arrange
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        var user = await CreateTestUser();

        var topScore = _mocker.Score.GetBestScoreableRandomScore();
        topScore.EnrichWithUserData(user);
        topScore.Mods = Mods.None;
        topScore.GameMode = GameMode.Standard;
        topScore.Grade = ScoreGrade.S;
        topScore.TotalScore = 1_000_000;
        topScore.MaxCombo = 300;
        topScore = await CreateTestScore(topScore);

        var otherScore = _mocker.Score.GetBestScoreableRandomScore();
        otherScore.EnrichWithUserData(user);
        otherScore.BeatmapHash = topScore.BeatmapHash;
        otherScore.BeatmapId = topScore.BeatmapId;
        otherScore.Mods = Mods.Hidden;
        otherScore.GameMode = GameMode.Standard;
        otherScore.Grade = ScoreGrade.A;
        otherScore.TotalScore = 500_000;
        otherScore.MaxCombo = 500;
        await CreateTestScore(otherScore);

        var stats = (await Database.Users.Stats.GetUserStats(user.Id, GameMode.Standard))!;
        stats.RankedScore = topScore.TotalScore;
        stats.MaxCombo = otherScore.MaxCombo;
        var grades = (await Database.Users.Grades.GetUserGrades(user.Id, GameMode.Standard))!;
        grades.CountS = 1;
        await Database.DbContext.SaveChangesAsync();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(topScore);
        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => beatmapSet);
        App.MockHttpClient.MockPerformanceCalculation(performancePoints: 150);

        // Act
        beatmap.StatusString = "pending";
        await RunChecks();
        await ProcessQueue();
        var pending = await ReloadUserState(topScore);

        beatmap.StatusString = "loved";
        await RunChecks();
        await ProcessQueue();
        var loved = await ReloadUserState(topScore);

        beatmap.StatusString = "ranked";
        await RunChecks();
        await ProcessQueue();
        var ranked = await ReloadUserState(topScore);

        // Assert
        Assert.Equal((0, 0, 0, 0), pending);
        Assert.Equal((0, 500, 1, 0), loved);
        Assert.Equal((1_000_000, 500, 1, 0), ranked);
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
    }

    [Fact]
    public async Task TestSubmissionBeforePendingStatusChangeCountsTopScoreOnce()
    {
        // Arrange
        var (submitted, queueEntry, beatmapSet, beatmap) = await ArrangeBetterSubmission(BeatmapStatus.Pending);
        beatmap.StatusString = "ranked";
        await RunChecks();
        Assert.True(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync(t => t.TaskType == ScoreTaskType.BeatmapStatusChange));

        // Act
        await SubmitScore(queueEntry, beatmapSet);
        await ProcessQueue();

        // Assert
        var persisted = (await Database.Scores.GetScore(submitted.ScoreHash))!;
        var (rankedScore, gradesTotal, submittedGradeCount) = await ReloadTotals(persisted);
        Assert.Equal((persisted.TotalScore, 1, 1), (rankedScore, gradesTotal, submittedGradeCount));
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
    }

    [Fact]
    public async Task TestSubmissionThatChangesStatusCountsTopScoreOnce()
    {
        // Arrange
        var (submitted, queueEntry, beatmapSet, beatmap) = await ArrangeBetterSubmission(BeatmapStatus.Pending);
        beatmap.StatusString = "ranked";

        // Act
        await SubmitScore(queueEntry, beatmapSet);
        await ProcessQueue();

        // Assert
        var persisted = (await Database.Scores.GetScore(submitted.ScoreHash))!;
        var (rankedScore, gradesTotal, submittedGradeCount) = await ReloadTotals(persisted);
        Assert.Equal((persisted.TotalScore, 1, 1), (rankedScore, gradesTotal, submittedGradeCount));
    }

    [Fact]
    public async Task TestRecalculationWithStaleTrackedStatusStillAppliesStatusChange()
    {
        // Arrange
        var score = await CreateRankedMapScoreCountedForUser(ScoreGrade.S, 1_000_000);
        await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash);

        var beatmapSet = MockUpstreamStatus(score, "pending").BeatmapSet;
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        App.MockHttpClient!.MockPerformanceCalculation(performancePoints: 100);
        using (var otherScope = App.Server.Services.CreateScope())
            await otherScope.ServiceProvider.GetRequiredService<DatabaseService>().Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Pending);

        var recalculation = new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Recalculation,
            ScoreId = score.Id,
            Status = ScoreProcessingStatus.Processing,
            ClaimToken = "worker",
            LeaseExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(recalculation);

        // Act
        var result = await Scope.ServiceProvider.GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.Recalculation).ExecuteAsync(recalculation, CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();

        // Assert
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Assert.Equal((0, 0, 0, 0), await ReloadUserState(score));
    }

    [Fact]
    public async Task TestStatusApplyWithStaleTrackedRowDoesNotQueueChangeAgain()
    {
        // Arrange
        var score = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash);

        using (var otherScope = App.Server.Services.CreateScope())
            await otherScope.ServiceProvider.GetRequiredService<DatabaseService>().Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Ranked);
        await Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>().ProcessQueue(CancellationToken.None);

        // Act
        await Database.Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Ranked);
        await ProcessQueue();

        // Assert
        Assert.Equal((1_000_000, score.MaxCombo, 1, 0), await ReloadUserState(score));
    }

    [Fact]
    public async Task TestSubmissionWithoutPendingStatusChangeCountsTopScoreOnce()
    {
        // Arrange
        var (submitted, queueEntry, beatmapSet, _) = await ArrangeBetterSubmission(BeatmapStatus.Ranked);

        // Act
        await SubmitScore(queueEntry, beatmapSet);
        await ProcessQueue();

        // Assert
        var persisted = (await Database.Scores.GetScore(submitted.ScoreHash))!;
        var (rankedScore, gradesTotal, submittedGradeCount) = await ReloadTotals(persisted);
        Assert.Equal((persisted.TotalScore, 1, 1), (rankedScore, gradesTotal, submittedGradeCount));
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
    }

    [Fact]
    public async Task TestDeleteAfterStatusChangeRemovesWhatItAdded()
    {
        // Arrange
        var score = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        MockUpstreamStatus(score, "ranked");
        await RunChecks();
        await ProcessQueue();
        Assert.Equal((1_000_000, score.MaxCombo, 1, 0), await ReloadUserState(score));

        // Act
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([score.Id], ScoreTaskType.Delete, ScoreProcessingPriority.High);
        await ProcessQueue();

        // Assert
        Assert.Equal((0, 0, 0, 0), await ReloadUserState(score));
    }

    [Fact]
    public async Task TestDeleteBeforeStatusChangeTaskLeavesNothingCounted()
    {
        // Arrange
        var score = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        MockUpstreamStatus(score, "ranked");
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([score.Id], ScoreTaskType.Delete, ScoreProcessingPriority.High);
        await RunChecks();

        // Act
        await ProcessQueue();

        // Assert
        Assert.Equal((0, 0, 0, 0), await ReloadUserState(score));
        Assert.False(await Database.DbContext.ScoreProcessingTasks.NotCacheable().AnyAsync());
    }

    [Fact]
    public async Task TestSecondStatusChangeWaitsUntilFirstOneIsApplied()
    {
        // Arrange
        var score = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        var (beatmap, _) = MockUpstreamStatus(score, "ranked");
        await RunChecks();
        await ProcessQueue();

        beatmap.StatusString = "pending";
        await RunChecks();

        // Act
        beatmap.StatusString = "loved";
        await RunChecks();
        var blockedHashStatus = (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!;

        await ProcessQueue();
        await RunChecks();
        await ProcessQueue();

        // Assert
        Assert.Equal(BeatmapStatus.Pending, blockedHashStatus.Status);
        Assert.True(blockedHashStatus.CheckedAt > DateTime.UtcNow.AddHours(-1));
        Assert.Equal(BeatmapStatus.Loved, (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!.Status);
        Assert.Equal((0, score.MaxCombo, 1, 0), await ReloadUserState(score));
    }

    [Fact]
    public async Task TestStatusChangeStillAppliesWhenItsScoreWasDeleted()
    {
        // Arrange
        var deletedScore = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        var keptScore = _mocker.Score.GetBestScoreableRandomScore();
        keptScore.EnrichWithUserData((await Database.Users.GetUser(deletedScore.UserId))!);
        keptScore.BeatmapHash = deletedScore.BeatmapHash;
        keptScore.BeatmapId = deletedScore.BeatmapId;
        keptScore.Mods = Mods.Hidden;
        keptScore.GameMode = GameMode.Standard;
        keptScore.Grade = ScoreGrade.A;
        keptScore.TotalScore = 500_000;
        keptScore.SubmissionStatus = SubmissionStatus.Submitted;
        keptScore.SetBeatmapStatus(BeatmapStatus.Pending);
        keptScore = await CreateTestScore(keptScore);

        MockUpstreamStatus(deletedScore, "ranked");
        await RunChecks();
        await Database.DbContext.Scores.Where(s => s.Id == deletedScore.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SubmissionStatus, SubmissionStatus.Deleted));

        // Act
        await ProcessQueue();

        // Assert
        Assert.Equal((500_000, keptScore.MaxCombo, 0, 1), await ReloadUserState(keptScore));
    }

    [Fact]
    public async Task TestStatusChangeClaimedByAnotherWorkerIsAppliedOnce()
    {
        // Arrange
        var topScore = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        var otherScore = await AddPendingMapScore(topScore, ScoreGrade.A, 500_000);
        MockUpstreamStatus(topScore, "ranked");
        await RunChecks();
        var statusChange = await ClaimStatusChange("other-worker");

        // Act
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([otherScore.Id], ScoreTaskType.Delete, ScoreProcessingPriority.High);
        await ProcessQueue();
        var otherWorkerResult = await Scope.ServiceProvider.GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.BeatmapStatusChange).ExecuteAsync(statusChange, CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();

        // Assert
        Assert.True(otherWorkerResult.IsFailure);
        Assert.Equal((1_000_000, topScore.MaxCombo, 1, 0), await ReloadUserState(topScore));
    }

    [Fact]
    public async Task TestCommittedStatusChangeIsNotAppliedAgainByNextCommit()
    {
        // Arrange
        var topScore = await CreatePendingMapScore(ScoreGrade.S, 1_000_000);
        var otherScore = await AddPendingMapScore(topScore, ScoreGrade.A, 500_000);
        MockUpstreamStatus(topScore, "ranked");
        await RunChecks();
        var statusChange = await ClaimStatusChange("worker");

        // Act
        var result = await Scope.ServiceProvider.GetRequiredKeyedService<IScoreHandler>(ScoreTaskType.BeatmapStatusChange).ExecuteAsync(statusChange, CancellationToken.None);
        Database.DbContext.ChangeTracker.Clear();
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([otherScore.Id], ScoreTaskType.Delete, ScoreProcessingPriority.High);
        await ProcessQueue();

        // Assert
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Assert.Equal((1_000_000, topScore.MaxCombo, 1, 0), await ReloadUserState(topScore));
    }

    [Fact]
    public async Task TestStatusUpdateWaitsForInFlightCommitOnTheMap()
    {
        // Arrange
        var score = await CreateUserScore();
        using var inFlightScope = App.Server.Services.CreateScope();
        var inFlightDatabase = inFlightScope.ServiceProvider.GetRequiredService<DatabaseService>();
        await using var inFlightCommit = await inFlightDatabase.DbContext.Database.BeginTransactionAsync();
        await inFlightDatabase.Calculations.LockBeatmapHashStatus(score.BeatmapHash);

        // Act
        var statusUpdate = Database.Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Pending);
        var finishedBeforeCommit = await Task.WhenAny(statusUpdate, Task.Delay(TimeSpan.FromSeconds(2))) == statusUpdate;
        await inFlightCommit.CommitAsync();
        await statusUpdate;

        // Assert
        Assert.False(finishedBeforeCommit);
        Assert.Equal(BeatmapStatus.Pending, (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!.Status);
    }

    [Fact]
    public async Task TestStatusChangingSubmissionsTakeTheMapLockOneAtATime()
    {
        // Arrange
        var score = await CreateUserScore();
        using var firstScope = App.Server.Services.CreateScope();
        var firstDatabase = firstScope.ServiceProvider.GetRequiredService<DatabaseService>();
        await using var firstCommit = await firstDatabase.DbContext.Database.BeginTransactionAsync();
        await firstDatabase.Calculations.LockBeatmapHashStatus(score.BeatmapHash, BeatmapStatus.Loved);

        // Act
        await using var secondCommit = await Database.DbContext.Database.BeginTransactionAsync();
        var secondLock = Database.Calculations.LockBeatmapHashStatus(score.BeatmapHash, BeatmapStatus.Loved);
        var lockedBeforeFirstCommit = await Task.WhenAny(secondLock, Task.Delay(TimeSpan.FromSeconds(2))) == secondLock;
        await firstCommit.CommitAsync();
        await secondLock;

        // Assert
        Assert.False(lockedBeforeFirstCommit);
    }

    [Fact]
    public async Task TestSubmissionsWithoutStatusChangeShareTheMapLock()
    {
        // Arrange
        var score = await CreateUserScore();
        using var firstScope = App.Server.Services.CreateScope();
        var firstDatabase = firstScope.ServiceProvider.GetRequiredService<DatabaseService>();
        await using var firstCommit = await firstDatabase.DbContext.Database.BeginTransactionAsync();
        await firstDatabase.Calculations.LockBeatmapHashStatus(score.BeatmapHash, BeatmapStatus.Ranked);

        // Act
        await using var secondCommit = await Database.DbContext.Database.BeginTransactionAsync();
        var secondLock = Database.Calculations.LockBeatmapHashStatus(score.BeatmapHash, BeatmapStatus.Ranked);
        var lockedBeforeFirstCommit = await Task.WhenAny(secondLock, Task.Delay(TimeSpan.FromSeconds(2))) == secondLock;
        await firstCommit.CommitAsync();

        // Assert
        Assert.True(lockedBeforeFirstCommit);
    }

    [Fact]
    public async Task TestCheckWaitingForTheMapLockSeesStatusChangeCommittedMeanwhile()
    {
        // Arrange
        var score = await CreateRankedMapScoreCountedForUser(ScoreGrade.S, 1_000_000);
        using var submissionScope = App.Server.Services.CreateScope();
        var submissionDatabase = submissionScope.ServiceProvider.GetRequiredService<DatabaseService>();
        await using var submission = await submissionDatabase.DbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await submissionDatabase.Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Pending);

        await Database.DbContext.Database.OpenConnectionAsync();
        var checkConnectionId = await GetConnectionId(Database.DbContext);

        // Act
        var check = Database.Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Loved);
        await WaitUntilConnectionWaitsForALock(checkConnectionId);
        await submission.CommitAsync();
        await check;
        await Database.DbContext.Database.CloseConnectionAsync();
        Database.DbContext.ChangeTracker.Clear();

        // Assert
        var hashStatus = (await Database.Calculations.GetBeatmapHashStatus(score.BeatmapHash))!;
        Assert.Equal((BeatmapStatus.Pending, BeatmapStatus.Ranked), (hashStatus.Status, hashStatus.PreviousStatus));
    }

    [Fact]
    public async Task TestStatusChangeTaskCannotBeCancelledButOtherTasksCan()
    {
        // Arrange
        var score = await CreateRankedMapScoreCountedForUser(ScoreGrade.S, 1_000_000);
        await Database.Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Pending);
        var statusChange = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.TaskType == ScoreTaskType.BeatmapStatusChange);
        var otherScore = await CreateUserScore();
        var recalculation = (await Database.ScoreProcessingTasks.BulkAddScoreTasks([otherScore.Id], ScoreTaskType.Recalculation, ScoreProcessingPriority.Low)).Single();

        // Act
        var statusChangeCancel = await Database.ScoreProcessingTasks.CancelTask(statusChange.Id);
        var recalculationCancel = await Database.ScoreProcessingTasks.CancelTask(recalculation.Id);

        // Assert
        Assert.True(statusChangeCancel.IsFailure);
        Assert.True(recalculationCancel.IsSuccess);
        Assert.Equal(ScoreProcessingStatus.Pending, (await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == statusChange.Id)).Status);
    }

    [Fact]
    public async Task TestBulkActionsQueueScoresWithPendingStatusChangeButSkipBusyScores()
    {
        // Arrange
        var score = await CreateRankedMapScoreCountedForUser(ScoreGrade.S, 1_000_000);
        await Database.Calculations.ApplyBeatmapHashStatus(score.BeatmapHash, score.BeatmapId, BeatmapStatus.Pending);
        var busyScore = await CreateUserScore();
        await Database.ScoreProcessingTasks.BulkAddScoreTasks([busyScore.Id], ScoreTaskType.Recalculation, ScoreProcessingPriority.Low);

        // Act
        var queued = await Database.ScoreProcessingTasks.BulkAddScoreTasks([score.Id, busyScore.Id], ScoreTaskType.Delete, ScoreProcessingPriority.Normal);

        // Assert
        Assert.Equal([score.Id], queued.Select(t => t.ScoreId!.Value));
    }

    [Fact]
    public async Task TestStatusChangeTaskKeepsRetryingPastMaxRetries()
    {
        // Arrange
        var score = await CreateUserScore();
        var statusChangeTask = await AddClaimedTaskAtMaxRetries(score, ScoreTaskType.BeatmapStatusChange);

        // Act
        await Database.ScoreProcessingTasks.TryMarkClaimedAsFailed(statusChangeTask.Id, "claim", new ScoreProcessingError(ScoreProcessingErrorCode.TransactionFailed, "failed", ScoreProcessingDisposition.Retryable), TimeSpan.Zero);

        // Assert
        Assert.Equal(ScoreProcessingStatus.Pending, (await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == statusChangeTask.Id)).Status);
    }

    [Fact]
    public async Task TestRecalculationTaskStillFailsPastMaxRetries()
    {
        // Arrange
        var score = await CreateUserScore();
        var recalculationTask = await AddClaimedTaskAtMaxRetries(score, ScoreTaskType.Recalculation);

        // Act
        await Database.ScoreProcessingTasks.TryMarkClaimedAsFailed(recalculationTask.Id, "claim", new ScoreProcessingError(ScoreProcessingErrorCode.TransactionFailed, "failed", ScoreProcessingDisposition.Retryable), TimeSpan.Zero);

        // Assert
        Assert.Equal(ScoreProcessingStatus.Failed, (await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == recalculationTask.Id)).Status);
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
            .AnyAsync(t => t.ScoreId == score.Id && t.TaskType == ScoreTaskType.BeatmapStatusChange && t.Status == ScoreProcessingStatus.Pending));
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

    private static async Task<long> GetConnectionId(SunriseDbContext dbContext)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CONNECTION_ID()";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task WaitUntilConnectionWaitsForALock(long connectionId)
    {
        using var scope = App.Server.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<SunriseDbContext>().Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT COUNT(*) FROM performance_schema.data_lock_waits w
            JOIN performance_schema.threads t ON t.THREAD_ID = w.REQUESTING_THREAD_ID
            WHERE t.PROCESSLIST_ID = {connectionId}";

        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) > 0)
                return;

            await Task.Delay(50);
        }

        Assert.Fail($"Connection {connectionId} never started waiting for a lock");
    }

    private async Task<Score> CreatePendingMapScore(ScoreGrade grade, long totalScore)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(await CreateTestUser());
        score.Mods = Mods.None;
        score.GameMode = GameMode.Standard;
        score.Grade = grade;
        score.TotalScore = totalScore;
        score.SubmissionStatus = SubmissionStatus.Submitted;
        score.SetBeatmapStatus(BeatmapStatus.Pending);
        return await CreateTestScore(score);
    }

    private async Task<Score> AddPendingMapScore(Score sameMapScore, ScoreGrade grade, long totalScore)
    {
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = sameMapScore.UserId;
        score.BeatmapHash = sameMapScore.BeatmapHash;
        score.BeatmapId = sameMapScore.BeatmapId;
        score.Mods = Mods.Hidden;
        score.GameMode = sameMapScore.GameMode;
        score.Grade = grade;
        score.TotalScore = totalScore;
        score.SubmissionStatus = SubmissionStatus.Submitted;
        score.SetBeatmapStatus(BeatmapStatus.Pending);
        return await CreateTestScore(score);
    }

    private async Task<ScoreProcessingTask> ClaimStatusChange(string claimToken)
    {
        var statusChange = await Database.DbContext.ScoreProcessingTasks.SingleAsync(t => t.TaskType == ScoreTaskType.BeatmapStatusChange);
        statusChange.Status = ScoreProcessingStatus.Processing;
        statusChange.ClaimToken = claimToken;
        statusChange.LeaseExpiresAt = DateTime.UtcNow.AddHours(1);
        await Database.DbContext.SaveChangesAsync();
        Database.DbContext.ChangeTracker.Clear();
        return statusChange;
    }

    private async Task<Score> CreateRankedMapScoreCountedForUser(ScoreGrade grade, long totalScore)
    {
        var score = await CreatePendingMapScore(grade, totalScore);
        await Database.DbContext.BeatmapHashStatuses.Where(h => h.BeatmapHash == score.BeatmapHash)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, BeatmapStatus.Ranked));
        await Database.DbContext.Scores.Where(s => s.Id == score.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SubmissionStatus, SubmissionStatus.Best));

        var stats = (await Database.Users.Stats.GetUserStats(score.UserId, score.GameMode))!;
        stats.RankedScore = totalScore;
        stats.MaxCombo = score.MaxCombo;
        var grades = (await Database.Users.Grades.GetUserGrades(score.UserId, score.GameMode))!;
        grades.UpdateGradeCount(grade, 1);
        await Database.DbContext.SaveChangesAsync();
        Database.DbContext.ChangeTracker.Clear();
        return score;
    }

    private (Beatmap Beatmap, BeatmapSet BeatmapSet) MockUpstreamStatus(Score score, string status)
    {
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.EnrichWithScoreData(score);
        beatmap.StatusString = status;
        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => beatmapSet);
        return (beatmap, beatmapSet);
    }

    private async Task<(Score Submitted, ScoreSubmissionRequest QueueEntry, BeatmapSet BeatmapSet, Beatmap Beatmap)> ArrangeBetterSubmission(BeatmapStatus existingStatus)
    {
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        var (session, user) = await CreateTestSession();
        var (replay, beatmapId) = GetValidTestReplay();
        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps!.First();
        beatmap.StatusString = existingStatus == BeatmapStatus.Ranked ? "ranked" : "pending";

        var submitted = replay.GetScore();
        submitted.BeatmapId = beatmapId;
        submitted.EnrichWithSessionData(session);
        submitted.PrepareForSubmission(beatmap);
        beatmap.EnrichWithScoreData(submitted);

        var queueEntry = ScoreSubmissionRequestTestDataFactory.CreateQueueEntry(submitted, user.Username, replayFileId: await CreateReplayFileId(user.Id));
        await Database.ScoreSubmissionRequests.AddQueueEntry(queueEntry);

        var existing = _mocker.Score.GetBestScoreableRandomScore();
        existing.EnrichWithUserData(user);
        existing.Mods = submitted.Mods;
        existing.GameMode = submitted.GameMode;
        existing.PrepareForSubmission(beatmap);
        existing.TotalScore = submitted.TotalScore - 100;
        existing.Grade = ScoreGrade.A;
        existing.SubmissionStatus = existingStatus.IsScoreable() ? SubmissionStatus.Best : SubmissionStatus.Submitted;
        existing.SetBeatmapStatus(existingStatus);
        existing = await CreateTestScore(existing);

        if (existingStatus.IsRanked())
        {
            var stats = (await Database.Users.Stats.GetUserStats(user.Id, existing.GameMode))!;
            stats.RankedScore = existing.TotalScore;
            var grades = (await Database.Users.Grades.GetUserGrades(user.Id, existing.GameMode))!;
            grades.UpdateGradeCount(existing.Grade, 1);
            await Database.DbContext.SaveChangesAsync();
        }

        App.MockHttpClient!.MockResponse(ApiType.BeatmapSetDataByHash, _ => beatmapSet);
        App.MockHttpClient.MockPerformanceCalculation(performancePoints: 100);
        return (submitted, queueEntry, beatmapSet, beatmap);
    }

    private async Task SubmitScore(ScoreSubmissionRequest queueEntry, BeatmapSet beatmapSet)
    {
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        var result = await Scope.ServiceProvider.GetRequiredService<ScoreSubmissionHandler>().ExecuteAsync(new ScoreProcessingTask
            {
                TaskType = ScoreTaskType.Submission,
                ScoreSubmissionRequestId = queueEntry.Id
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Database.DbContext.ChangeTracker.Clear();
    }

    private async Task<ScoreProcessingTask> AddClaimedTaskAtMaxRetries(Score score, ScoreTaskType taskType)
    {
        var task = (await Database.ScoreProcessingTasks.BulkAddScoreTasks([score.Id], taskType, ScoreProcessingPriority.High)).Single();
        await Database.DbContext.ScoreProcessingTasks.Where(t => t.Id == task.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, ScoreProcessingStatus.Processing)
                .SetProperty(t => t.ClaimToken, "claim")
                .SetProperty(t => t.RetryCount, Configuration.ScoreProcessingMaxRetries - 1));
        return task;
    }

    private async Task<(long RankedScore, int GradesTotal, int SubmittedGradeCount)> ReloadTotals(Score score)
    {
        var stats = await Database.DbContext.UserStats.NotCacheable().AsNoTracking().SingleAsync(s => s.UserId == score.UserId && s.GameMode == score.GameMode);
        var grades = await Database.DbContext.UserGrades.NotCacheable().AsNoTracking().SingleAsync(g => g.UserId == score.UserId && g.GameMode == score.GameMode);
        var gradesTotal = grades.CountXH + grades.CountX + grades.CountSH + grades.CountS + grades.CountA + grades.CountB + grades.CountC + grades.CountD;
        return (stats.RankedScore, gradesTotal, grades.GetGradeCount(score.Grade));
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

    private async Task<(long RankedScore, int MaxCombo, int CountS, int CountA)> ReloadUserState(Score score)
    {
        var stats = await Database.DbContext.UserStats.NotCacheable().AsNoTracking().SingleAsync(s => s.UserId == score.UserId && s.GameMode == score.GameMode);
        var grades = await Database.DbContext.UserGrades.NotCacheable().AsNoTracking().SingleAsync(g => g.UserId == score.UserId && g.GameMode == score.GameMode);
        return (stats.RankedScore, stats.MaxCombo, grades.CountS, grades.CountA);
    }

    private async Task<Score> ReloadScore(Score score)
    {
        Database.DbContext.ChangeTracker.Clear();
        return await Database.DbContext.Scores.NotCacheable().AsNoTracking().SingleAsync(s => s.Id == score.Id);
    }
}
