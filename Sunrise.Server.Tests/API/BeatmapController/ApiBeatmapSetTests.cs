using System.Net;
using System.Net.Http.Json;
using Sunrise.API.Serializable.Request;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
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
    
    [Fact]
    public async Task TestGetBeatmapSetWithCustomBeatmapStatus()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);
        
        EnvManager.Set("General:IgnoreBeatmapRanking", "false");
        
        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var addCustomBeatmapStatusResult = await Database.Beatmaps.CustomStatuses.AddCustomBeatmapStatus(new CustomBeatmapStatus()
        {
            Status = BeatmapStatusWeb.Loved,
            BeatmapHash = beatmapSet.Beatmaps.First().Checksum ?? throw new InvalidOperationException(),
            BeatmapSetId = beatmapSet.Id,
            UpdatedByUserId = user.Id,
        });
        
        if (addCustomBeatmapStatusResult.IsFailure)
            throw new Exception(addCustomBeatmapStatusResult.Error);

        // Act
        var response = await client.GetAsync("beatmapset/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var beatmapSetResponse = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetResponse>();
        Assert.NotNull(beatmapSetResponse);
        
        Assert.Equal(beatmapSet.Id, beatmapSetResponse.Id);
        Assert.Equal(BeatmapStatusWeb.Loved, beatmapSetResponse.Status);
        
        Assert.Equal(user.Id, beatmapSetResponse.BeatmapNominatorUser?.Id);
        
        Assert.False(beatmapSetResponse.CanBeHyped);
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
        var response = await client.PostAsJsonAsync($"beatmapset/{beatmapSet.Id}/favourited",
            new EditBeatmapsetFavouriteStatusRequest
            {
                Favourited = true
            });

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
        var response = await client.PostAsJsonAsync($"beatmapset/{beatmapSet.Id}/favourited",
            new EditBeatmapsetFavouriteStatusRequest
            {
                Favourited = favouriteBeatmapSetAfterAct
            }
        );

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