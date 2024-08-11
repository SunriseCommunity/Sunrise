using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePublic)]
public class ChatMessagePublicHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = session.User.Username,
            SenderId = session.User.Id
        };

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        sessions.WriteToAllSessions(PacketType.ServerChatMessage, message, session.User.Id);
        return Task.CompletedTask;
    }
}