using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Controllers;

public class BanchoControllerTests : DatabaseTest
{
    [Fact]
    public async Task Get_ReturnsImage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }
}