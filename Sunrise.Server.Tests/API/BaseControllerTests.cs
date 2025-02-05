using System.Text.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Repositories;
using Sunrise.Server.Tests.Utils;

namespace Sunrise.Server.Tests.API;

[Collection(nameof(SystemCollectionsWithoutParallelization))]
public class BaseControllerTests
{
    [Fact]
    public async Task Ping_ReturnsOk()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("/ping");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var responseString = await response.Content.ReadAsStringAsync();
        Assert.Equal("Sunrise API", responseString);
    }
    
    [Fact]
    public async Task Status_Returns_Info()
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
}