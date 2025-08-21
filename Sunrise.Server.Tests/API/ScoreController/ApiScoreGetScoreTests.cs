using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.ScoreController;

public class ApiScoreGetScoreTests : ApiTest
{
    [Fact]
    public async Task TestGetValidScore()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var score = await CreateTestScore(user);

        score.User = user;

        // Act
        var response = await client.GetAsync($"score/{score.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseScore = await response.Content.ReadFromJsonAsyncWithAppConfig<ScoreResponse>();
        var scoreData = new ScoreResponse(Sessions, score);

        Assert.Equivalent(responseScore, scoreData);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task TestGetNotExistingScore(object id)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"score/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();

        Assert.Equal(ApiErrorResponse.Title.ValidationError, responseString?.Title);
    }

    [Fact]
    public async Task TestGetScoreOfRestrictedPlayer()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var score = await CreateTestScore(user);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.GetAsync($"score/{score.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.ScoreNotFound, responseString?.Detail);
    }

    [Fact]
    public async Task TestGetInvalidScore()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("score/invalid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}