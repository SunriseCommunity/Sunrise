using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiBeatmapFavouritedTests : ApiTest
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestBeatmapSetFavouritedBeatmapId(string beatmapSetId)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}/favourited");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestGetBeatmapSetFavourited(bool favouriteBeatmapSetBeforeAct)
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var beatmapSetId = _mocker.GetRandomInteger();

        if (favouriteBeatmapSetBeforeAct)
            await database.UserService.Favourites.AddFavouriteBeatmap(user.Id, beatmapSetId);

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}/favourited");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsync<FavouritedResponse>();
        Assert.NotNull(responseString);

        Assert.Equal(favouriteBeatmapSetBeforeAct, responseString.Favourited);
    }
}