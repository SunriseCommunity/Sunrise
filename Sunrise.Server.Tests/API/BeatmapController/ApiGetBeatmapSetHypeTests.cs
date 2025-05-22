using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Application;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiGetBeatmapSetHypeRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestGetBeatmapSetHype(bool shouldHypeBefore)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        if (shouldHypeBefore)
        {
            var user = await CreateTestUser();
            var addBeatmapHypeResult = await Database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSet.Id);
            if (addBeatmapHypeResult.IsFailure)
                throw new Exception(addBeatmapHypeResult.Error);
        }

        // Act
        var response = await client.GetAsync("beatmapset/1/hype");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetHypeCountResponse>();
        Assert.NotNull(content);

        Assert.Equal(shouldHypeBefore ? 1 : 0, content.CurrentHypes);
        Assert.Equal(Configuration.HypesToStartHypeTrain, content.RequiredHypes);
    }
}

public class ApiGetBeatmapSetHypeTests() : ApiTest(true)
{
    [Theory]
    [InlineData("-1")]
    public async Task TestGetBeatmapSetHypeInvalidBeatmapSetId(string beatmapSetId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmapset/{beatmapSetId}/hype");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapSetHypeNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/1/hype");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}