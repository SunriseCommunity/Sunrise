using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Chat;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePublic)]
public class ChatMessagePublicHandler : IPacketHandler
{
    private readonly ChatRateLimiter _rateLimiter = new(5, TimeSpan.FromSeconds(4));

    public async Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = session.User.Username,
            SenderId = session.User.Id
        };

        if (!_rateLimiter.CanSend(session)) return;

        var channels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
        var channel = channels.GetChannel(session, message.Channel);

        channel?.SendToChannel(message.Message, session.User.Username);

        if (message.Message.StartsWith(Configuration.BotPrefix))
            await ChatCommandRepository.HandleCommand(message, session);
    }
}