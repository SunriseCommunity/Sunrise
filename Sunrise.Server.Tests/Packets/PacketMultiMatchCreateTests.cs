using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Repositories.Multiplayer;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Packets;

[Collection("Integration tests collection")]
public class PacketMultiMatchCreateTests(IntegrationDatabaseFixture fixture) : BanchoTest(fixture)
{
    [Fact]
    public async Task TestShouldCreateMultiMatch()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var loginToken = await GetUserAuthOsuToken(client, user);
        client.UseUserAuthOsuToken(loginToken);

        var newMatch = new BanchoMultiplayerMatch
        {
            GameName = "Test Match",
            MultiType = MultiTypes.Standard,
            BeatmapName = "Test Beatmap",
            BeatmapChecksum = "abc123",
            BeatmapId = 0,
            InProgress = false,
            ActiveMods = Mods.HardRock | Mods.DoubleTime,
            HostId = user.Id,
            PlayMode = GameMode.Standard,
            MultiWinCondition = MultiWinConditions.ScoreV2,
            MultiTeamType = MultiTeamTypes.HeadToHead,
            SpecialModes = MultiSpecialModes.None,
            Seed = 0,
            GamePassword = "testpassword"
        };

        PacketHelper.WritePacket(PacketType.ClientMultiMatchCreate, newMatch);
        var requestBody = PacketHelper.GetBytesToSend();

        // Act
        var response = await client.PostAsync("/", new ByteArrayContent(requestBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var matchRepository = App.Services.GetRequiredService<MatchRepository>();

        Assert.Contains(matchRepository.GetMatches(), x => x.Match.GameName == newMatch.GameName && x.Match.HostId == newMatch.HostId);
    }

    [Fact]
    public async Task TestShouldCreateMultiMatchAndAddToUserSession()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var loginToken = await GetUserAuthOsuToken(client, user);
        client.UseUserAuthOsuToken(loginToken);

        var newMatch = new BanchoMultiplayerMatch
        {
            GameName = "Test Match",
            MultiType = MultiTypes.Standard,
            BeatmapName = "Test Beatmap",
            BeatmapChecksum = "abc123",
            BeatmapId = 0,
            InProgress = false,
            ActiveMods = Mods.HardRock | Mods.DoubleTime,
            HostId = user.Id,
            PlayMode = GameMode.Standard,
            MultiWinCondition = MultiWinConditions.ScoreV2,
            MultiTeamType = MultiTeamTypes.HeadToHead,
            SpecialModes = MultiSpecialModes.None,
            Seed = 0,
            GamePassword = "testpassword"
        };

        PacketHelper.WritePacket(PacketType.ClientMultiMatchCreate, newMatch);
        var requestBody = PacketHelper.GetBytesToSend();

        // Act
        var response = await client.PostAsync("/", new ByteArrayContent(requestBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var sessionRepository = App.Services.GetRequiredService<SessionRepository>();

        Assert.NotNull(sessionRepository.GetSession(userId: user.Id)?.Match);
    }

    [Fact]
    public async Task TestShouldCreateMultiMatchOnceWithMultipleRequests()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var loginToken = await GetUserAuthOsuToken(client, user);
        client.UseUserAuthOsuToken(loginToken);

        var newMatch = new BanchoMultiplayerMatch
        {
            GameName = "Test Match",
            MultiType = MultiTypes.Standard,
            BeatmapName = "Test Beatmap",
            BeatmapChecksum = "abc123",
            BeatmapId = 0,
            InProgress = false,
            ActiveMods = Mods.HardRock | Mods.DoubleTime,
            HostId = user.Id,
            PlayMode = GameMode.Standard,
            MultiWinCondition = MultiWinConditions.ScoreV2,
            MultiTeamType = MultiTeamTypes.HeadToHead,
            SpecialModes = MultiSpecialModes.None,
            Seed = 0,
            GamePassword = "testpassword"
        };

        for (var i = 0; i < 5; i++)
        {
            PacketHelper.WritePacket(PacketType.ClientMultiMatchCreate, newMatch);
        }

        var requestBody = PacketHelper.GetBytesToSend();

        // Act
        var response = await client.PostAsync("/", new ByteArrayContent(requestBody));

        // Assert
        response.EnsureSuccessStatusCode();

        var matchRepository = App.Services.GetRequiredService<MatchRepository>();

        Assert.Single(matchRepository.GetMatches());
    }
}