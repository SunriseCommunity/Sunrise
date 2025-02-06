using System.Net;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.ScoreController;

public class ApiScoreGetScoreTests : ApiTest
{
    [Fact]
    public async Task TestGetValidScore()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var score = await CreateTestScore(user);

        // Act
        var response = await client.GetAsync($"score/{score.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var responseScore = await response.Content.ReadFromJsonAsync<ScoreResponse>();
        var scoreData = new ScoreResponse(score, user);

        Assert.Equivalent(responseScore, scoreData);
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9999)]
    public async Task TestGetNotExistingScore(object id)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        
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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        
        var user = await CreateTestUser();
        var score = await CreateTestScore(user);
        
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.UserService.Moderation.RestrictPlayer(user.Id, 0, "Test");
        
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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        
        // Act
        var response = await client.GetAsync("score/invalid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}