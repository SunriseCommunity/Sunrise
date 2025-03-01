using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Controllers;

public class AssetsControllerTests : DatabaseTest
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