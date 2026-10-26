using System.Net;
using osu.Shared;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminGetUserScoresAdminTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetGameModes() => Enum.GetValues<GameMode>().Select(mode => new object[] { mode });

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
        hiddenScore.Mods = Mods.Hidden;
        await Database.Scores.AddScore(hiddenScore);

        var noModScore = _mocker.Score.GetBestScoreableRandomScore();
        noModScore.EnrichWithUserData(targetUser);
        noModScore.Mods = Mods.None;
        await Database.Scores.AddScore(noModScore);

        var hiddenBit = (int)Mods.Hidden;

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

    [Theory]
    [MemberData(nameof(GetGameModes))]
    public async Task TestGetUserScoresAdminPerformanceSortKeepsOlderEqualPpFirst(GameMode mode)
    {
        var client = App.CreateClient().UseClient("api");
        var superUser = _mocker.User.GetRandomUser();
        superUser.Privilege = UserPrivilege.SuperUser;
        await CreateTestUser(superUser);
        client.UseUserAuthToken(await GetUserAuthTokens(superUser));

        var targetUser = await CreateTestUser();
        var earlierScore = _mocker.Score.GetBestScoreableRandomScore();
        earlierScore.EnrichWithUserData(targetUser);
        earlierScore.GameMode = mode;
        earlierScore.PerformancePoints = 100;
        earlierScore.WhenPlayed = DateTime.UtcNow.AddMinutes(-10);
        earlierScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(earlierScore);

        var laterScore = _mocker.Score.GetBestScoreableRandomScore();
        laterScore.EnrichWithUserData(targetUser);
        laterScore.GameMode = mode;
        laterScore.PerformancePoints = earlierScore.PerformancePoints;
        laterScore.WhenPlayed = earlierScore.WhenPlayed.AddMinutes(1);
        laterScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(laterScore);

        var lowerScore = _mocker.Score.GetBestScoreableRandomScore();
        lowerScore.EnrichWithUserData(targetUser);
        lowerScore.GameMode = mode;
        lowerScore.PerformancePoints = 50;
        lowerScore.WhenPlayed = earlierScore.WhenPlayed.AddMinutes(2);
        lowerScore.ScoreHash = Guid.NewGuid().ToString("N");
        await Database.Scores.AddScore(lowerScore);

        var response = await client.GetAsync($"user/{targetUser.Id}/scores/admin?mode={(int)mode}&sort=Performance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsyncWithAppConfig<AdminScoresResponse>();
        Assert.NotNull(result);
        Assert.Equal(new[] { earlierScore.Id, laterScore.Id, lowerScore.Id }, result.Scores.Select(score => score.Score.Id));
    }
}
