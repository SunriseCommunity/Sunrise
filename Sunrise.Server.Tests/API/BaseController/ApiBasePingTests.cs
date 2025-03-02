using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BaseController;

public class ApiBasePingTests : ApiTest
{
    [Fact]
    public async Task TestPingReturnsOk()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("/ping");

        // Assert
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        Assert.Equal("Sunrise API", responseString);
    }
}