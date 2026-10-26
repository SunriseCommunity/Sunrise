using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreProcessingController;

[Collection("Integration tests collection")]
public class ApiScoreProcessingPreviewTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestPreviewWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("score-processing/score/1");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestPreviewWithMissingScore()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("score-processing/score/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestPreviewReturnsScoreWithoutActiveTask()
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
        var response = await client.GetAsync($"score-processing/score/{score.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(score.Id, preview.Score.Score.Id);
        Assert.Null(preview.ActiveTask);
    }

    [Fact]
    public async Task TestPreviewReturnsDeletedScoreWithActiveTask()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var scoreUser = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(scoreUser);
        score.SubmissionStatus = SubmissionStatus.Deleted;
        await Database.Scores.AddScore(score);

        await Database.ScoreProcessingTasks.AddQueueEntry(new ScoreProcessingTask
        {
            TaskType = ScoreTaskType.Restore,
            ScoreId = score.Id,
            Priority = (int)ScoreProcessingPriority.Normal,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await client.GetAsync($"score-processing/score/{score.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreProcessingPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(score.Id, preview.Score.Score.Id);
        Assert.Equal(SubmissionStatus.Deleted, preview.Score.SubmissionStatus);
        Assert.NotNull(preview.ActiveTask);
        Assert.Equal(ScoreTaskType.Restore, preview.ActiveTask.TaskType);
    }
}
