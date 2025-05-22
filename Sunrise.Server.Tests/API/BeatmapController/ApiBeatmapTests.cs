using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiBeatmapRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmap()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var beatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        beatmap.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        // Act
        var response = await client.GetAsync("beatmap/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapWithCustomStatus()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        var randomBeatmap = beatmapSet.Beatmaps.First() ?? throw new Exception("Beatmap is null");
        randomBeatmap.Id = 1;
        randomBeatmap.StatusString = BeatmapStatusWeb.Pending.BeatmapStatusWebToString();

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        EnvManager.Set("General:IgnoreBeatmapRanking", "false");

        var randomUser = _mocker.User.GetRandomUser();
        await Database.Users.AddUser(randomUser);

        await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus
        {
            Status = BeatmapStatusWeb.Loved,
            BeatmapHash = randomBeatmap.Checksum,
            BeatmapSetId = beatmapSet.Id,
            UpdatedByUserId = randomUser.Id
        });

        // Act
        var response = await client.GetAsync("beatmap/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var beatmap = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapResponse>();

        Assert.NotNull(beatmap);

        Assert.Equal(BeatmapStatusWeb.Loved, beatmap.Status);
    }
}

public class ApiBeatmapTests : ApiTest
{
    [Theory]
    [InlineData("-1")]
    [InlineData("test")]
    public async Task TestGetBeatmapInvalidBeatmapId(string beatmapId)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync($"beatmap/{beatmapId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapNotFound()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmap/1");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}