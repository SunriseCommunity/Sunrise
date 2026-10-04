using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Repositories.Multiplayer;
using Sunrise.Tests.Abstracts;

namespace Sunrise.Shared.Tests.Repositories;

[Collection("Integration tests collection")]
public class SessionRepositoryTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestRemoveSessionRemovesSessionFromMultiplayerLobby()
    {
        var matches = App.Services.GetRequiredService<MatchRepository>();
        var user = await CreateTestUser();
        var session = CreateTestSession(user);

        matches.JoinLobby(session);

        Assert.Equal(1, matches.GetLobbySessionCount());

        await Sessions.RemoveSession(session);

        Assert.Equal(0, matches.GetLobbySessionCount());
    }

    [Fact]
    public async Task TestRemoveSessionRemovesSessionFromAbstractChannels()
    {
        var channels = App.Services.GetRequiredService<ChatChannelRepository>();
        var user = await CreateTestUser();
        var session = CreateTestSession(user);
        var initialChannelCount = channels.GetChannelCount(includeAbstract: true);

        channels.JoinChannel("#multiplayer_1", session, true);

        Assert.Equal(initialChannelCount + 1, channels.GetChannelCount(includeAbstract: true));

        await Sessions.RemoveSession(session);

        Assert.Equal(initialChannelCount, channels.GetChannelCount(includeAbstract: true));
    }
}
