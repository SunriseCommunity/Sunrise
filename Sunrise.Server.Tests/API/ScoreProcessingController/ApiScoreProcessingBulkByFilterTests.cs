using System.Net;
using System.Net.Http.Json;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Request;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Jobs;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingBulkByFilterTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestBulkByFilterWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk-by-filter",
            new BulkScoreProcessingByFilterRequest
            {
                Action = ScoreTaskType.Delete,
                UserId = 1
            });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestBulkByFilterRejectsInvalidAction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk-by-filter",
            new BulkScoreProcessingByFilterRequest
            {
                Action = ScoreTaskType.Submission,
                UserId = 1
            });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InvalidScoreProcessingAction, responseError?.Detail);
    }

    [Fact]
    public async Task TestBulkByFilterQueuesBackgroundJobToCreateScoreProcessingTask()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var score = await CreateTestScore();

        // Act
        var response = await client.PostAsJsonAsync("score-processing/bulk-by-filter",
            new BulkScoreProcessingByFilterRequest
            {
                Mode = score.GameMode,
                UserId = score.UserId,
                Action = ScoreTaskType.Delete
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var api = JobStorage.Current.GetMonitoringApi();
        var enqueued = api.EnqueuedJobs("default", 0, 100);

        Assert.NotEmpty(enqueued);

        Assert.Contains(enqueued, job => job.Value.Job.Type == typeof(BulkScoreProcessingJob));
    }
}