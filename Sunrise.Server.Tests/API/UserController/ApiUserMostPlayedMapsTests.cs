using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserMostPlayedMapsRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestMostPlayedMaps()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        var score1 = _mocker.Score.GetBestScoreableRandomScore();
        score1.EnrichWithBeatmapData(beatmap);
        score1.EnrichWithUserData(user);
        score1.GameMode = (GameMode)beatmap.ModeInt;
        await CreateTestScore(score1);

        var score2 = _mocker.Score.GetBestScoreableRandomScore();
        score2.EnrichWithBeatmapData(beatmap);
        score2.EnrichWithUserData(user);
        score2.GameMode = (GameMode)beatmap.ModeInt;
        await CreateTestScore(score2);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?mode={beatmap.ModeInt}");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<MostPlayedResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.MostPlayed);
        Assert.Contains(responseData.MostPlayed, b => b.Id == beatmap.Id);
        Assert.Equal(2, responseData.MostPlayed.First().PlayCount);
    }

    [Fact]
    public async Task TestMostPlayedMapsLimitAndPage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var gameMode = _mocker.Score.GetRandomGameMode();

        Score? firstAddedScore = null;

        for (var i = 0; i < 2; i++)
        {
            var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
            var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
            beatmap.ModeInt = (int)gameMode.ToVanillaGameMode();
            await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

            var score = _mocker.Score.GetBestScoreableRandomScore();
            score.EnrichWithBeatmapData(beatmap);
            score.EnrichWithUserData(user);
            score.GameMode = gameMode;
            score.WhenPlayed = DateTime.MinValue.AddSeconds(i);

            await CreateTestScore(score);

            if (firstAddedScore == null)
                firstAddedScore = score;
        }

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?mode={(int)gameMode}&limit=1&page=2");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<MostPlayedResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.MostPlayed);
        Assert.Single(responseData.MostPlayed);

        Assert.Contains(responseData.MostPlayed, b => b.Id == firstAddedScore!.BeatmapId);

        Assert.Equal(2, responseData.TotalCount);
    }
}

public class ApiUserMostPlayedMapsTests : ApiTest
{
    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestMostPlayedMapsInvalidUserId(string userId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/mostplayed");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestMostPlayedMapsInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestMostPlayedMapsInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestMostPlayedMapsWithoutBeatmapSet()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var score = await CreateTestScore();

        // Act
        var response = await client.GetAsync($"user/{score.UserId}/mostplayed");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<MostPlayedResponse>();
        Assert.NotNull(responseData);

        Assert.Empty(responseData.MostPlayed);
    }

    [Fact]
    public async Task TestMostPlayedMapsForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseContent?.Detail);
    }
}