using System.Net;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiBeatmapSetRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapSet()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var response = await client.GetAsync("beatmapset/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapSetNotFound()
    {
        // Arrange
        await using var app = new SunriseServerFactory();
        var client = app.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/1");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}