﻿using System.Net;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserFavouritesRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestFavourites()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var beatmapSet = await _mocker.Beatmap.MockRandomBeatmapSet();

        await database.UserService.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet.Id);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<BeatmapSetsResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.Sets);
        Assert.Contains(responseData.Sets, set => set.Id == beatmapSet.Id);
    }

    [Fact]
    public async Task TestFavouritesLimitAndPage()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var beatmapSet = await _mocker.Beatmap.MockRandomBeatmapSet();
        await database.UserService.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet.Id);

        var beatmapSet2 = await _mocker.Beatmap.MockRandomBeatmapSet();
        await database.UserService.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet2.Id);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites?limit=1&page=1");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<BeatmapSetsResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.Sets);
        Assert.Single(responseData.Sets);
        Assert.Contains(responseData.Sets, set => set.Id == beatmapSet2.Id);

        Assert.Equal(2, responseData.TotalCount);
    }
}

public class ApiUserFavouritesTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestFavouritesInvalidUserId(string userId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/favourites");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestFavouritesInvalidLimit(string limit)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites?limit={limit}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestFavouritesUserInvalidPage(string page)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites?page={page}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseData = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("invalid", responseData?.Error.ToLower());
    }

    [Fact]
    public async Task TestFavouritesWithoutBeatmapSet()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Favourites.AddFavouriteBeatmap(user.Id, _mocker.GetRandomInteger());

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<BeatmapSetsResponse>();
        Assert.NotNull(responseData);

        Assert.Empty(responseData.Sets);
    }

    [Fact]
    public async Task TestFavouritesForRestrictedUser()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("User is restricted", responseError?.Error);
    }
}