using Sunrise.Server.Tests.Utils;

namespace Sunrise.Server.Tests.Controllers;

[Collection(nameof(SystemCollectionsWithoutParallelization))]
public class BanchoControllerTests
{
    [Fact]
    public async Task Get_ReturnsImage()
    {
        // Arrange
        var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("c");
    
        // Act
        var response = await client.GetAsync("/");
    
        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }
}