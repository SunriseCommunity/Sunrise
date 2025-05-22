using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiGetHypedBeatmapSetsRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetHypedBeatmapSetsEmpty()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        // Act
        var response = await client.GetAsync("beatmapset/get-hyped-sets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<HypedBeatmapSetsResponse>();
        Assert.NotNull(content);

        Assert.Empty(content.Sets);
        Assert.Equal(0, content.TotalCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestGetHypedBeatmapSets(bool shouldAddedHypeStartHypeTrain)
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        var beatmapSet = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSet.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSet);

        EnvManager.Set("BeatmapHype:HypesToStartHypeTrain", shouldAddedHypeStartHypeTrain ? "1" : "100");

        await Database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSet.Id);

        // Act
        var response = await client.GetAsync("beatmapset/get-hyped-sets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<HypedBeatmapSetsResponse>();
        Assert.NotNull(content);

        Assert.Equal(shouldAddedHypeStartHypeTrain ? 1 : 0, content.Sets.Count);
        Assert.Equal(shouldAddedHypeStartHypeTrain ? 1 : 0, content.TotalCount);

        if (shouldAddedHypeStartHypeTrain)
            Assert.Equal(beatmapSet.Id, content.Sets.First().Id);
    }

    [Fact]
    public async Task TestGetHypedBeatmapSetsTestPageAndLimit()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        EnvManager.Set("BeatmapHype:HypesToStartHypeTrain", "1");

        var beatmapSetFirst = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSetFirst.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSetFirst);
        await Database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSetFirst.Id);

        var beatmapSetSecond = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSetSecond.Id = 2;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSetSecond);
        await Database.Beatmaps.Hypes.AddBeatmapHypeFromUserInventory(user, beatmapSetSecond.Id);

        // Act
        var response = await client.GetAsync("beatmapset/get-hyped-sets?limit=1&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<HypedBeatmapSetsResponse>();
        Assert.NotNull(content);

        Assert.Single(content.Sets);
        Assert.Equal(2, content.TotalCount);
        Assert.Equal(beatmapSetFirst.Id, content.Sets.First().Id);
    }
}

public class ApiGetHypedBeatmapSetsTests() : ApiTest(true)
{
    [Fact]
    public async Task TestGetHypedBeatmapSetsUnauthorized()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/get-hyped-sets");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetHypedBeatmapSetsForbiddenIfHasNoPrivileges()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("beatmapset/get-hyped-sets");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}