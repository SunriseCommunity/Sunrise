using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.BaseController;

public class ApiBasePingTests : ApiTest
{
    [Fact]
    public async Task TestPingReturnsOk()
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
}