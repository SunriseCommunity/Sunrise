using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sunrise.Server.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Tests.Utils;

namespace Sunrise.Server.Tests.API;

public class AuthControllerTests : ApiTest
{
    [Fact]
    public async Task Get_User_Token()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsJsonAsync("auth/token",
            new
            {
                username = "user",
                password = "password"
            });

        // Assert
        response.EnsureSuccessStatusCode();

        var responseToken = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", responseToken.Token);
        
        Assert.NotNull(responseToken.Token);
    }
}