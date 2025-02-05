using Sunrise.Server.Tests.Utils;

namespace Sunrise.Server.Tests.Controllers;

[Collection(nameof(SystemCollectionsWithoutParallelization))]
public class AssetsControllerTests
{
    [Fact]
    public async Task Get_Bot_Avatar()
    {
        // Arrange
        var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("a");
    
        // Act
        var response = await client.GetAsync($"/avatar/1");
    
        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }
}