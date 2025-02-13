using System.Text.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Repositories;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.BaseController;

public class ApiBaseStatusTests : ApiTest
{
    [Fact]
    public async Task TestStatusReturnsValidInfo()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        
        // Act
        var response = await client.GetAsync("/status");
        
        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseString = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<StatusResponse>(responseString);
        
        Assert.NotNull(status);
        
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        
        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await database.UserService.GetTotalUsers();
        
        Assert.Equal(usersOnline, status.UsersOnline);
        Assert.Equal(totalUsers, status.TotalUsers);
    }
    
    [Fact]
    public async Task TestStatusDetailedReturnsValidInfo()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");
        
        // Act
        var response = await client.GetAsync("/status?detailed=true");
        
        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseString = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<StatusResponse>(responseString);
        
        Assert.NotNull(status);
        
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        
        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await database.UserService.GetTotalUsers();
        var totalScores = await database.ScoreService.GetTotalScores();
        
        Assert.Equal(usersOnline, status.UsersOnline);
        Assert.Equal(totalUsers, status.TotalUsers);
        Assert.Equal(totalScores, status.TotalScores);
    }
}