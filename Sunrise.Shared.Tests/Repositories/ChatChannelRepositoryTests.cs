using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Repositories;
using Sunrise.Tests.Abstracts;

namespace Sunrise.Shared.Tests.Repositories;

[Collection("Integration tests collection")]
public class ChatChannelRepositoryTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestLeaveChannelRemovesEmptyMultiplayerAbstractChannel()
    {
        var channels = App.Services.GetRequiredService<ChatChannelRepository>();
        var user = await CreateTestUser();
        var session = CreateTestSession(user);
        var initialChannelCount = channels.GetChannelCount(includeAbstract: true);

        channels.JoinChannel("#multiplayer_1", session, true);
        channels.LeaveChannel("#multiplayer_1", session, true);

        Assert.Equal(initialChannelCount, channels.GetChannelCount(includeAbstract: true));
    }

    [Fact]
    public async Task TestLeaveChannelRemovesEmptySpectatorAbstractChannel()
    {
        var channels = App.Services.GetRequiredService<ChatChannelRepository>();
        var user = await CreateTestUser();
        var session = CreateTestSession(user);
        var initialChannelCount = channels.GetChannelCount(includeAbstract: true);

        channels.JoinChannel("#spectator_1", session, true);
        channels.LeaveChannel("#spectator_1", session, true);

        Assert.Equal(initialChannelCount, channels.GetChannelCount(includeAbstract: true));
    }
}
