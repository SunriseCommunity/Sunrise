using System.Net;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.BeatmapController;

public class ApiGetBeatmapSetsEventsRedisTests() : ApiTest(true)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestGetBeatmapSetsEvents()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        var beatmapSetFirst = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSetFirst.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSetFirst);
        await Database.Events.Beatmaps.AddBeatmapSetHypeEvent(user.Id, beatmapSetFirst.Id);
        
        var beatmapSetSecond = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSetSecond.Id = 2;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSetSecond);
        await Database.Events.Beatmaps.AddBeatmapSetHypeEvent(user.Id, beatmapSetSecond.Id);

        // Act
        var response = await client.GetAsync("beatmapset/events");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetEventsResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(2, content.TotalCount);
        
        Assert.Equal(2, events.Count(e => e.BeatmapEventType == BeatmapEventType.BeatmapSetHyped));
    }
    
    [Fact]
    public async Task TestGetBeatmapSetsEventsTestPageAndLimit()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        user.Privilege = UserPrivilege.Bat;
        await Database.Users.UpdateUser(user);

        var beatmapSetFirst = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSetFirst.Id = 1;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSetFirst);
        
        for (var i = 0; i < 2; i++)
        {
            await Database.Events.Beatmaps.AddBeatmapSetHypeEvent(user.Id, beatmapSetFirst.Id);
        }
        
        var beatmapSetSecond = _mocker.Beatmap.GetRandomBeatmapSet();
        beatmapSetSecond.Id = 2;

        await _mocker.Beatmap.MockBeatmapSet(beatmapSetSecond);
        await Database.Events.Beatmaps.AddBeatmapSetHypeEvent(user.Id, beatmapSetSecond.Id);

        // Act
        var response = await client.GetAsync("beatmapset/events?limit=1&page=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsyncWithAppConfig<BeatmapSetEventsResponse>();
        Assert.NotNull(content);

        var events = content.Events;

        Assert.NotEmpty(events);
        Assert.Equal(3, content.TotalCount);

        Assert.Equal(2, events.First().EventId);
    }
}

public class ApiGetBeatmapSetsEventsTests() : ApiTest(true)
{
    [Fact]
    public async Task TestGetBeatmapSetsEventsUnauthorized()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.GetAsync("beatmapset/events");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestGetBeatmapSetsEventsForbiddenIfHasNoPrivileges()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.GetAsync("beatmapset/events");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}