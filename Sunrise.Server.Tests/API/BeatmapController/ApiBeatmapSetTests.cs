using System.Net;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiBeatmapSetRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapSet()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var response = await client.GetAsync("beatmapset/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class ApiBeatmapSetFavouriteRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapSetUpdateFavouriteInvalidSession()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSet.Id}?favourite=true");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task TestGetBeatmapSetUpdateFavourite(bool favouriteBeatmapSetBeforeAct, bool favouriteBeatmapSetAfterAct)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        if (favouriteBeatmapSetBeforeAct)
            await Database.Users.Favourites.AddFavouriteBeatmap(user.Id, beatmapSet.Id);

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSet.Id}?favourite={favouriteBeatmapSetAfterAct}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var isBeatmapSetFavourited = await Database.Users.Favourites.IsBeatmapSetFavourited(user.Id, beatmapSet.Id);
        Assert.Equal(favouriteBeatmapSetAfterAct, isBeatmapSetFavourited);
    }
}

public class ApiBeatmapSetTests : ApiTest
{
    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetBeatmapSetInvalidBeatmapSetId(string beatmapSetId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapSetNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/1");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}