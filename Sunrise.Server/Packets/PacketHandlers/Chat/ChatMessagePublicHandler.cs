using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Chat;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePublic)]
public class ChatMessagePublicHandler : IPacketHandler
{
    private readonly ChatRateLimiter _rateLimiter = new(5, TimeSpan.FromSeconds(4));

    public async Task Handle(BanchoPacket packet, Session session)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(id: session.UserId);
        if (user == null)
            return;
        
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = user.Username,
            SenderId = user.Id
        };

        if (!_rateLimiter.CanSend(session)) return;

        var channels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
        var channel = channels.GetChannel(session, message.Channel);

        channel?.SendToChannel(message.Message, user);

        if (message.Message.StartsWith(Configuration.BotPrefix))
            await ChatCommandRepository.HandleCommand(message, session);
    }
}