using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserFavouritesRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestFavourites()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var beatmapSet = await _mocker.Beatmap.MockRandomBeatmapSet();

        await Database.Users.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet.Id);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();
        Assert.NotNull(responseData);

        Assert.NotEmpty(responseData.Sets);
        Assert.Contains(responseData.Sets, set => set.Id == beatmapSet.Id);
    }

    [Fact]
    public async Task TestFavouritesLimitAndPage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        var beatmapSet = await _mocker.Beatmap.MockRandomBeatmapSet();
        await Database.Users.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet.Id);

        var beatmapSet2 = await _mocker.Beatmap.MockRandomBeatmapSet();
        await Database.Users.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet2.Id);

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites?limit=1&page=2");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();
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
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"user/{userId}/favourites");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("test")]
    public async Task TestFavouritesInvalidLimit(string limit)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites?limit={limit}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestFavouritesUserInvalidPage(string page)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites?page={page}");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestFavouritesWithoutBeatmapSet()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Favourites.AddFavouriteBeatmap(user.Id, _mocker.GetRandomInteger());

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetsResponse>();
        Assert.NotNull(responseData);

        Assert.Empty(responseData.Sets);
    }

    [Fact]
    public async Task TestFavouritesForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"user/{user.Id}/favourites");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserIsRestricted, responseError?.Detail);
    }
}