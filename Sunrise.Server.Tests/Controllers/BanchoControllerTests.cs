using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.Controllers;

public class BanchoControllerTests : DatabaseTest
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