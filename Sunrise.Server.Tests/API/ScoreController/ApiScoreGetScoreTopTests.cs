using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreController;

public class ApiScoreGetScoreTopTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetTopScoresInvalidGameMode(string gameMode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"score/top?mode={gameMode}&limit=15");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("200")]
    [InlineData("test")]
    public async Task TestGetTopScoresInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"score/top?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetTopScoresInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"score/top?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetTopScoresForEmptyModeUseDefault()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var gamemode = GameMode.Standard;

        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.UserId = user.Id;
        score.GameMode = gamemode;
        await Database.Scores.AddScore(score);

        // Act
        var response = await client.GetAsync("score/top");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scores = await response.Content.ReadFromJsonAsync<ScoresResponse>();

        Assert.NotNull(scores);
        Assert.Single(scores.Scores);
    }

    [Fact]
    public async Task TestGetMultipleTopScores()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var gamemode = _mocker.Score.GetRandomGameMode();

        var randomInt = new Random().Next(1, 10);

        for (var i = 0; i < randomInt; i++)
        {
            var user = await CreateTestUser();
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.UserId = user.Id;
            score.GameMode = gamemode;

            await Database.Scores.AddScore(score);
        }

        // Act
        var response = await client.GetAsync($"score/top?mode={(int)gamemode}&limit=15");

        // Assert
        response.EnsureSuccessStatusCode();
        var scores = await response.Content.ReadFromJsonAsync<ScoresResponse>();
        Assert.NotNull(scores);

        Assert.Equal(randomInt, scores.Scores.Count);
    }

    [Fact]
    public async Task TestGetOnlySingleTopScore()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var gamemode = _mocker.Score.GetRandomGameMode();

        var randomInt = new Random().Next(5, 10);

        for (var i = 0; i < randomInt; i++)
        {
            var user = await CreateTestUser();
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.UserId = user.Id;
            score.GameMode = gamemode;

            await Database.Scores.AddScore(score);
        }

        // Act
        var response = await client.GetAsync($"score/top?mode={(int)gamemode}&limit=1");

        // Assert
        response.EnsureSuccessStatusCode();
        var scores = await response.Content.ReadFromJsonAsync<ScoresResponse>();
        Assert.NotNull(scores);

        Assert.Single(scores.Scores);
    }

    [Fact]
    public async Task TestGetTopScoresIgnoreRestrictedUsers()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var gamemode = _mocker.Score.GetRandomGameMode();

        var randomInt = new Random().Next(1, 10);

        for (var i = 0; i < randomInt; i++)
        {
            var user = await CreateTestUser();
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.UserId = user.Id;
            score.GameMode = gamemode;

            await Database.Scores.AddScore(score);
        }

        var users = await Database.Users.GetUsers();
        var restrictedUser = users.Last();

        await Database.Users.Moderation.RestrictPlayer(restrictedUser.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"score/top?mode={(int)gamemode}&limit=15");

        // Assert
        response.EnsureSuccessStatusCode();
        var scores = await response.Content.ReadFromJsonAsync<ScoresResponse>();
        Assert.NotNull(scores);

        Assert.Equal(randomInt - 1, scores.Scores.Count);
    }

    [Fact]
    public async Task TestGetTopScoresIgnoreScoreIfItDoesntHasUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var gamemode = _mocker.Score.GetRandomGameMode();

        var randomInt = new Random().Next(1, 10);

        for (var i = 0; i < randomInt; i++)
        {
            var user = await CreateTestUser();
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.UserId = user.Id;
            score.GameMode = gamemode;

            await Database.Scores.AddScore(score);
        }

        var scoreWithoutUser = _mocker.Score.GetRandomScore();
        scoreWithoutUser.UserId = -1;
        scoreWithoutUser.GameMode = gamemode;
        await Database.Scores.AddScore(scoreWithoutUser);

        // Act
        var response = await client.GetAsync($"score/top?mode={(int)gamemode}&limit=15");

        // Assert
        response.EnsureSuccessStatusCode();
        var scores = await response.Content.ReadFromJsonAsync<ScoresResponse>();
        Assert.NotNull(scores);

        Assert.Equal(randomInt, scores.Scores.Count);
    }
}