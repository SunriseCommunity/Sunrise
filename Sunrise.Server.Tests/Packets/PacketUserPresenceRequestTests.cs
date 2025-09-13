using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Packets;

public class PacketUserPresenceRequestTests : BanchoTest
{
    [Fact]
    public async Task TestReturnExpectedPresence()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var loginToken = await GetUserAuthOsuToken(client, user);
        client.UseUserAuthOsuToken(loginToken);

        var expectedPresence = new BanchoUserPresence
        {
            UserId = user.Id,
            Username = user.Username,
            UsesOsuClient = true,
            Latitude = 0,
            Longitude = 0,
            Permissions = PlayerRank.Default,
            Rank = 1,
            Timezone = 0,
            CountryCode = (byte)user.Country,
            PlayMode = GameMode.Standard
        };

        PacketHelper.WritePacket(PacketType.ClientUserPresenceRequest, 0);
        var requestBody = PacketHelper.GetBytesToSend();

        // Act
        var response = await client.PostAsync("/", new ByteArrayContent(requestBody));
        
        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var responsePacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerUserPresence);
        Assert.NotNull(responsePacket);

        var responseData = new BanchoUserPresence(responsePacket.Data);
        Assert.NotNull(responseData);

        Assert.Equivalent(expectedPresence, responseData);
    }
}