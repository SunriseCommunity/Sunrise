using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserGetUserScoresTests : ApiTest
{
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetScoreTableTypes()
    {
        return Enum.GetValues(typeof(ScoreTableType)).Cast<ScoreTableType>().Select(type => new object[]
        {
            type
        });
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserScoresInvalidUserId(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/scores");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserScoresInvalidGameMode(string gamemode)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores?mode={gamemode}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("200")]
    [InlineData("test")]
    public async Task TestGetUserScoresInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetUserScoresInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetUserScoresForEmptyModeUseDefault()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var gamemode = GameMode.Standard;

        var user = await CreateTestUser();
        var score = _mocker.Score.GetBestScoreableRandomScore();
        score.EnrichWithUserData(user);
        score.GameMode = gamemode;
        await Database.Scores.AddScore(score);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scores = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();

        Assert.NotNull(scores);
        Assert.Single(scores.Scores);
    }

    [Theory]
    [MemberData(nameof(GetScoreTableTypes))]
    public async Task TestGetUserScoresByType(ScoreTableType type)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var gamemode = _mocker.Score.GetRandomGameMode();
        var user = await CreateTestUser();

        var scoreableScoresCount = new Random().Next(1, 3);

        for (var i = 0; i < scoreableScoresCount; i++)
        {
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithUserData(user);
            score.GameMode = gamemode;

            await Database.Scores.AddScore(score);
        }

        var unrankedScoresCount = new Random().Next(1, 3);

        for (var i = 0; i < unrankedScoresCount; i++)
        {
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithUserData(user);
            score.BeatmapStatus = BeatmapStatus.Loved;
            score.GameMode = gamemode;

            await Database.Scores.AddScore(score);
        }

        var failedScoresCount = new Random().Next(1, 3);

        for (var i = 0; i < failedScoresCount; i++)
        {
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithUserData(user);
            score.GameMode = gamemode;
            score.IsPassed = false;

            await Database.Scores.AddScore(score);
        }

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores?mode={(int)gamemode}&type={(int)type}&limit=10");

        // Assert
        response.EnsureSuccessStatusCode();

        var scores = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();
        Assert.NotNull(scores);

        var expectedScoresCount = type switch
        {
            ScoreTableType.Best => scoreableScoresCount,
            ScoreTableType.Top => scoreableScoresCount + unrankedScoresCount,
            ScoreTableType.Recent => scoreableScoresCount + unrankedScoresCount + failedScoresCount,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        Assert.Equal(expectedScoresCount, scores.Scores.Count);
    }

    [Fact]
    public async Task TestGetUserScoresWithLimitAndPage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var gamemode = GameMode.Standard;

        var user = await CreateTestUser();

        var lastScoreId = 0;

        for (var i = 0; i < 2; i++)
        {
            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithUserData(user);
            score.GameMode = gamemode;
            score.WhenPlayed = DateTime.MinValue.AddSeconds(i);
            await Database.Scores.AddScore(score);

            lastScoreId = score.Id;
        }

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores?limit=1&page=1&mode={(int)gamemode}&type={(int)ScoreTableType.Recent}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scores = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoresResponse>();

        Assert.NotNull(scores);
        Assert.Single(scores.Scores);
        Assert.Equal(lastScoreId, scores.Scores.First().Id);
    }

    [Fact]
    public async Task TestGetUserScoresForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/scores");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }
}