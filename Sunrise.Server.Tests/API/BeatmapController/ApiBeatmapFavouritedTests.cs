using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

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
        var client = App.CreateClient().UseClient("api").UseUserAuthToken(await GetUserAuthTokens());

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
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var beatmapSetId = _mocker.GetRandomInteger();

        if (favouriteBeatmapSetBeforeAct)
            await Database.Users.Favourites.AddFavouriteBeatmap(user.Id, beatmapSetId);

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}/favourited");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadFromJsonAsyncWithAppConfig<FavouritedResponse>();
        Assert.NotNull(responseString);

        Assert.Equal(favouriteBeatmapSetBeforeAct, responseString.Favourited);
    }
}