﻿using System.Net;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Extensions;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Extensions;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserMostPlayedMapsRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestMostPlayedMaps()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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

        var responseData = await response.Content.ReadFromJsonAsync<MostPlayedResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.MostPlayed);
        Assert.Contains(responseData.MostPlayed, b => b.Id == beatmap.Id);
        Assert.Equal(2, responseData.MostPlayed.First().PlayCount);
    }

    [Fact]
    public async Task TestMostPlayedMapsLimitAndPage()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var gameMode = _mocker.Score.GetRandomGameMode();

        var lastAddedBeatmapId = 0;

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
            await CreateTestScore(score);

            lastAddedBeatmapId = beatmap.Id;
        }

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?mode={(int)gameMode}&limit=1&page=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<MostPlayedResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.MostPlayed);
        Assert.Single(responseData.MostPlayed);
        Assert.Contains(responseData.MostPlayed, b => b.Id == lastAddedBeatmapId);

        Assert.Equal(2, responseData.TotalCount);
    }
}

public class ApiUserMostPlayedMapsTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestMostPlayedMapsInvalidUserId(string userId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/mostplayed");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestMostPlayedMapsInvalidLimit(string limit)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestMostPlayedMapsInvalidPage(string page)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Fact]
    public async Task TestMostPlayedMapsWithoutBeatmapSet()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var score = await CreateTestScore();

        // Act
        var response = await client.GetAsync($"user/{score.UserId}/mostplayed");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<MostPlayedResponse>();
        Assert.NotNull(responseData);

        Assert.Empty(responseData.MostPlayed);
    }

    [Fact]
    public async Task TestMostPlayedMapsForRestrictedUser()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/mostplayed");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User is restricted", responseError?.Error);
    }
}