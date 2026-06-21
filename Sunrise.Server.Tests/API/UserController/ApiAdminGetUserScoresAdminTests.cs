using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminGetUserScoresAdminTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetUserScoresAdminWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var targetUser = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/scores/admin");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserScoresAdminWithNonSuperUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/scores/admin");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserScoresAdminWithMissingUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("user/999999/scores/admin");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserScoresAdminIncludesDeletedScores()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var targetUser = await CreateTestUser();

        var bestScore = _mocker.Score.GetBestScoreableRandomScore();
        bestScore.EnrichWithUserData(targetUser);
        bestScore.SubmissionStatus = SubmissionStatus.Best;
        await Database.Scores.AddScore(bestScore);

        var deletedScore = _mocker.Score.GetBestScoreableRandomScore();
        deletedScore.EnrichWithUserData(targetUser);
        deletedScore.SubmissionStatus = SubmissionStatus.Deleted;
        await Database.Scores.AddScore(deletedScore);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/scores/admin");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<AdminScoresResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task TestGetUserScoresAdminFiltersByMods()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var targetUser = await CreateTestUser();

        var hiddenScore = _mocker.Score.GetBestScoreableRandomScore();
        hiddenScore.EnrichWithUserData(targetUser);
        hiddenScore.Mods = osu.Shared.Mods.Hidden | osu.Shared.Mods.HardRock;
        await Database.Scores.AddScore(hiddenScore);

        var noModScore = _mocker.Score.GetBestScoreableRandomScore();
        noModScore.EnrichWithUserData(targetUser);
        noModScore.Mods = osu.Shared.Mods.None;
        await Database.Scores.AddScore(noModScore);

        var hiddenBit = (int)osu.Shared.Mods.Hidden;

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/scores/admin?mods={hiddenBit}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<AdminScoresResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(hiddenScore.Id, result.Scores.Single().Score.Id);
    }

    [Fact]
    public async Task TestGetUserScoresAdminFiltersBySubmissionStatus()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);

        var tokens = await GetUserAuthTokens(superUser);
        client.UseUserAuthToken(tokens);

        var targetUser = await CreateTestUser();

        var bestScore = _mocker.Score.GetBestScoreableRandomScore();
        bestScore.EnrichWithUserData(targetUser);
        bestScore.SubmissionStatus = SubmissionStatus.Best;
        await Database.Scores.AddScore(bestScore);

        var deletedScore = _mocker.Score.GetBestScoreableRandomScore();
        deletedScore.EnrichWithUserData(targetUser);
        deletedScore.SubmissionStatus = SubmissionStatus.Deleted;
        await Database.Scores.AddScore(deletedScore);

        // Act
        var response = await client.GetAsync($"user/{targetUser.Id}/scores/admin?submission_status=Deleted");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<AdminScoresResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(SubmissionStatus.Deleted, result.Scores.Single().SubmissionStatus);
    }
}
