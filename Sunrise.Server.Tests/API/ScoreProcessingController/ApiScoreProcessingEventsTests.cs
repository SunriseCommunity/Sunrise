using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingEventsTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetEventsWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("score-processing/events");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetEventsWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("score-processing/events");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestGetEventsReturnsServerEventWithNullExecutor()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        await Database.Events.ScoreProcessing.AddSubmissionEnqueuedEvent(123, 456, 789);

        // Act
        var response = await client.GetAsync("score-processing/events");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<EventScoreProcessingListResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);

        var serverEvent = result.Events.Single();
        Assert.Equal(ScoreProcessingEventType.SubmissionEnqueued, serverEvent.EventType);
        Assert.Null(serverEvent.Executor);
    }

    [Fact]
    public async Task TestGetEventsFiltersByType()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var executor = await CreateTestUser();
        var score = await CreateTestScore();

        await Database.Events.ScoreProcessing.AddSubmissionEnqueuedEvent(1, 2, 3);
        await Database.Events.ScoreProcessing.AddActionRequestedEvent(executor.Id, score.Id, 99, ScoreTaskType.Delete, (int)ScoreProcessingPriority.Normal);

        // Act
        var response = await client.GetAsync("score-processing/events?types=DeleteRequested");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<EventScoreProcessingListResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(ScoreProcessingEventType.DeleteRequested, result.Events.Single().EventType);
    }

    [Fact]
    public async Task TestGetEventsFiltersByScoreId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var executor = await CreateTestUser();
        var score = await CreateTestScore();

        await Database.Events.ScoreProcessing.AddActionRequestedEvent(executor.Id, score.Id, 1, ScoreTaskType.Delete, (int)ScoreProcessingPriority.Normal);
        await Database.Events.ScoreProcessing.AddSubmissionEnqueuedEvent(1, 2, 3);

        // Act
        var response = await client.GetAsync($"score-processing/events?score_id={score.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<EventScoreProcessingListResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(score.Id, result.Events.Single().ScoreId);
    }
}
