using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreController;

public class ApiScoreGetScoreReplayTests : ApiTest
{
    [Fact]
    public async Task TestGetScoreReplay()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var score = await CreateTestScore();

        // Act
        var response = await client.GetAsync($"score/{score.Id}/replay");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task TestGetNotExistingScoreReplay(object id)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"score/{id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();

        Assert.Equal(ApiErrorResponse.Title.ValidationError, responseString?.Title);
    }

    [Fact]
    public async Task TestGetInvalidScoreReplay()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync("score/invalid/replay");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestGetScoreReplayUnauthorized()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var score = await CreateTestScore();

        // Act
        var response = await client.GetAsync($"score/{score.Id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetScoreReplayNotExistingReplay()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var score = await CreateTestScore(false);

        // Act
        var response = await client.GetAsync($"score/{score.Id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();

        Assert.Equal(ApiErrorResponse.Title.ValidationError, responseString?.Title);
    }

    [Fact]
    public async Task TestGetScoreReplayOfRestrictedPlayer()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var user = await CreateTestUser();
        var score = await CreateTestScore(user);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"score/{score.Id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}