using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
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
        var responseScore = await response.Content.ReadFromJsonAsync<ScoreResponse>();
        var scoreData = new ScoreResponse(score);

        Assert.Equivalent(responseScore, scoreData);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9999)]
    public async Task TestGetNotExistingScore(object id)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"score/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Score not found", responseContent?.Error);
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

        var responseContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("Score not found", responseContent?.Error);
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