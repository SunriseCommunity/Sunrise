using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePublic)]
public class ChatMessagePublicHandler : IHandler
{
    private readonly RateLimiter _rateLimiter = new(5, TimeSpan.FromSeconds(4));

    public Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = session.User.Username,
            SenderId = session.User.Id
        };

        if (!_rateLimiter.CanSend(session))
        {
            return Task.CompletedTask;
        }

        if (message.Message.StartsWith(Configuration.BotPrefix))
        {
            return CommandRepository.HandleCommand(message, session);
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        sessions.WriteToAllSessions(PacketType.ServerChatMessage, message, session.User.Id);
        return Task.CompletedTask;
    }
}