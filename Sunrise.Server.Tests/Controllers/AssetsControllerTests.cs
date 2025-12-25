using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.Controllers;

[Collection("Integration tests collection")]
public class AssetsControllerTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Get_Bot_Avatar()
    {
        // Arrange
        var client = App.CreateClient().UseClient("a");

        // Act
        var response = await client.GetAsync("/avatar/1");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }
}
