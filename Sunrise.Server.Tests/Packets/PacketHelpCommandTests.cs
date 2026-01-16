using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.Packets;

[Collection("Integration tests collection")]
public class PacketHelpCommandTests(IntegrationDatabaseFixture fixture) : BanchoTest(fixture)
{
    [Fact]
    public async Task TestHelpCommandResponse()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var loginToken = await GetUserAuthOsuToken(client, user);
        client.UseUserAuthOsuToken(loginToken);

        var packet = new BanchoChatMessage()
        {
            Channel = Configuration.BotUsername,
            Message = "!help",
            Sender = user.Username,
            SenderId = user.Id
        };

        PacketHelper.WritePacket(PacketType.ClientChatMessagePrivate,  packet);
        var requestBody = PacketHelper.GetBytesToSend();

        // Act
        var response = await client.PostAsync("/", new ByteArrayContent(requestBody));
        
        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var responsePacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerChatMessage);
        Assert.NotNull(responsePacket);

        var responseData = new BanchoChatMessage(responsePacket.Data);
        Assert.NotNull(responseData);

        Assert.Equal(Configuration.BotUsername, responseData.Sender);
        Assert.Equal(Configuration.BotUsername, responseData.Channel);
        Assert.Contains("Available commands:", responseData.Message);
    }
    
    [Fact]
    public async Task TestHelpCommandResponseAfterBotUsernameShouldWork()
    {
        // Arrange
        var client = App.CreateClient().UseClient("c");

        var user = await CreateTestUser();
        var loginToken = await GetUserAuthOsuToken(client, user);
        client.UseUserAuthOsuToken(loginToken);
        
        var botUser = await Database.Users.GetServerBot();
        botUser!.Username = $"some_username";
        await Database.Users.UpdateUser(botUser);

        await RecurringJobs.RefreshServerBotAccount(CancellationToken.None);
        

        var packet = new BanchoChatMessage()
        {
            Channel = botUser.Username,
            Message = "!help",
            Sender = user.Username,
            SenderId = user.Id
        };

        PacketHelper.WritePacket(PacketType.ClientChatMessagePrivate,  packet);
        var requestBody = PacketHelper.GetBytesToSend();
        


        // Act
        var response = await client.PostAsync("/", new ByteArrayContent(requestBody));
        
        // Assert
        response.EnsureSuccessStatusCode();

        var responsePackets = await GetResponsePackets(response);
        var responsePacket = responsePackets.FirstOrDefault(x => x.Type == PacketType.ServerChatMessage);
        Assert.NotNull(responsePacket);

        var responseData = new BanchoChatMessage(responsePacket.Data);
        Assert.NotNull(responseData);

        Assert.Equal(Configuration.BotUsername, responseData.Sender);
        Assert.Equal(Configuration.BotUsername, responseData.Channel);
        Assert.Contains("Available commands:", responseData.Message);
        
        
    }
}