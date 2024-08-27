using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePublic)]
public class ChatMessagePublicHandler : IHandler
{
    private readonly RateLimiter _rateLimiter = new(5, TimeSpan.FromSeconds(4));

    public async Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = session.User.Username,
            SenderId = session.User.Id
        };

        if (!_rateLimiter.CanSend(session))
        {
            return;
        }

        var channels = ServicesProviderHolder.ServiceProvider.GetRequiredService<ChannelRepository>();
        var channel = channels.GetChannel(session, message.Channel);

        channel?.SendToChannel(message.Message, session.User.Username);

        if (message.Message.StartsWith(Configuration.BotPrefix))
        {
            await CommandRepository.HandleCommand(message, session);
        }
    }
}