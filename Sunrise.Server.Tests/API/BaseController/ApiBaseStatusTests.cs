using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BaseController;

public class ApiBaseStatusTests : ApiTest
{
    [Fact]
    public async Task TestStatusReturnsValidInfo()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("/status");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<StatusResponse>(responseString);

        Assert.NotNull(status);

        var sessions = Scope.ServiceProvider.GetRequiredService<SessionRepository>();

        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await Database.Users.CountUsers();

        Assert.Equal(usersOnline, status.UsersOnline);
        Assert.Equal(totalUsers, status.TotalUsers);
    }

    [Fact]
    public async Task TestStatusDetailedReturnsValidInfo()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("/status?detailed=true");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var status = JsonSerializer.Deserialize<StatusResponse>(responseString);

        Assert.NotNull(status);

        var sessions = Scope.ServiceProvider.GetRequiredService<SessionRepository>();

        var usersOnline = sessions.GetSessions().Count;
        var totalUsers = await Database.Users.CountUsers();
        var totalScores = await Database.Scores.CountScores();

        Assert.Equal(usersOnline, status.UsersOnline);
        Assert.Equal(totalUsers, status.TotalUsers);
        Assert.Equal(totalScores, status.TotalScores);
    }
}