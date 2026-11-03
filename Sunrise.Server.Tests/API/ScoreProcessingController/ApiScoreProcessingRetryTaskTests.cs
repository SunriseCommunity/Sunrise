using System.Net;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Processing.Scores.Jobs;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingRetryTaskTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task RetryRequiresSuperUser(bool authenticated, HttpStatusCode expected)
    {
        var client = App.CreateClient().UseClient("api");
        if (authenticated)
            client.UseUserAuthToken(await GetUserAuthTokens());

        var response = await client.PostAsync("score-processing/1/retry", null);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(999999, HttpStatusCode.NotFound)]
    public async Task RetryRejectsInvalidOrMissingTask(int taskId, HttpStatusCode expected)
    {
        var (client, _) = await CreateAdminClient();

        var response = await client.PostAsync($"score-processing/{taskId}/retry", null);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(40)]
    public async Task RetryMakesPendingTaskDueWithoutResettingFailuresOrBypassingCooldown(int retries)
    {
        var (client, executorId) = await CreateAdminClient();
        var score = await CreateTestScore();
        var versionId = await Database.Calculations.GetOrCreateVersionId("3.2.0");
        var run = await Database.Calculations.StartRun(versionId!.Value);
        var task = new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Recalculation,
            ScoreId = score.Id,
            RunId = run.Id,
            RetryCount = retries,
            ErrorCode = ScoreProcessingErrorCode.PpCalculationFailed,
            ErrorMessage = "Osu file not found",
            NextRetryAt = DateTime.UtcNow.AddHours(1),
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(task);
        var redis = Scope.ServiceProvider.GetRequiredService<RedisRepository>();
        await redis.Set(RedisKey.ApiServerRateLimited(ApiServer.Observatory), true, TimeSpan.FromMinutes(1));
        var before = DateTime.UtcNow.AddSeconds(-1);

        var response = await client.PostAsync($"score-processing/{task.Id}/retry", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal(ScoreProcessingStatus.Pending, updated.Status);
        Assert.NotNull(updated.NextRetryAt);
        Assert.InRange(updated.NextRetryAt.Value, before, DateTime.UtcNow);
        Assert.Equal(retries, updated.RetryCount);
        Assert.Equal(task.ErrorCode, updated.ErrorCode);
        Assert.Equal(task.ErrorMessage, updated.ErrorMessage);
        Assert.Equal(task.Priority, updated.Priority);
        Assert.Equal(run.Id, updated.RunId);
        Assert.Null(updated.ClaimToken);
        Assert.Null(updated.LeaseExpiresAt);
        var (events, _) = await Database.Events.ScoreProcessing.GetEvents(types: [ScoreProcessingEventType.Requeued]);
        var audit = Assert.Single(events);
        Assert.Equal(task.Id, audit.TaskId);
        Assert.Equal(executorId, audit.ExecutorId);

        await Scope.ServiceProvider.GetRequiredService<ScoreProcessingJob>().ProcessQueue(CancellationToken.None);
        Assert.Equal(ScoreProcessingStatus.Pending,
            (await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == task.Id)).Status);
        await redis.Remove(RedisKey.ApiServerRateLimited(ApiServer.Observatory));
        Assert.Equal(task.Id, Assert.Single(await Database.ScoreProcessingTasks.ClaimPendingBatch(1, TimeSpan.FromMinutes(2))).Id);
    }

    [Theory]
    [InlineData(ScoreProcessingStatus.Processing)]
    [InlineData(ScoreProcessingStatus.Failed)]
    public async Task RetryDoesNotChangeTasksThatAreNotPending(ScoreProcessingStatus status)
    {
        var (client, _) = await CreateAdminClient();
        var score = await CreateTestScore();
        var task = new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Recalculation,
            ScoreId = score.Id,
            Status = status,
            RetryCount = 40,
            NextRetryAt = DateTime.UtcNow.AddHours(1),
            ClaimToken = status == ScoreProcessingStatus.Processing ? "current-owner" : null,
            CreatedAt = DateTime.UtcNow
        };
        await Database.ScoreProcessingTasks.AddQueueEntry(task);
        var original = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == task.Id);

        var response = await client.PostAsync($"score-processing/{task.Id}/retry", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var updated = await Database.DbContext.ScoreProcessingTasks.NotCacheable().AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal(status, updated.Status);
        Assert.Equal(original.NextRetryAt, updated.NextRetryAt);
        Assert.Equal(original.RetryCount, updated.RetryCount);
        Assert.Equal(original.ClaimToken, updated.ClaimToken);
        var (events, _) = await Database.Events.ScoreProcessing.GetEvents();
        Assert.Empty(events);
    }

    private async Task<(HttpClient, int)> CreateAdminClient()
    {
        var superUser = new MockService().User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);
        var client = App.CreateClient().UseClient("api");
        client.UseUserAuthToken(await GetUserAuthTokens(superUser));
        return (client, superUser.Id);
    }
}
