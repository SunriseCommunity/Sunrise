using System.Net;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;

namespace Sunrise.Server.Tests.API.ScoreController;

public class ApiScoreGetScoreReplayTests : ApiTest
{
    [Fact]
    public async Task TestGetScoreReplay()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

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
    [InlineData(9999)]
    public async Task TestGetNotExistingScoreReplay(object id)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"score/{id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestGetInvalidScoreReplay()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync("score/invalid/replay");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestGetScoreReplayUnauthorized()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var score = await CreateTestScore(false);

        // Act
        var response = await client.GetAsync($"score/{score.Id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TestGetScoreReplayOfRestrictedPlayer()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        var user = await CreateTestUser();
        var score = await CreateTestScore(user);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");

        // Act
        var response = await client.GetAsync($"score/{score.Id}/replay");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}