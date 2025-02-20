using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Chat;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePrivate)]
public class ChatMessagePrivateHandler : IPacketHandler
{
    private const string Action = "ACTION";
    private readonly ChatRateLimiter _rateLimiter = new(10, TimeSpan.FromSeconds(5));

    public async Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = session.User.Username,
            SenderId = session.User.Id
        };

        if (!_rateLimiter.CanSend(session)) return;

        if (message.Channel == Configuration.BotUsername)
            if (message.Message.StartsWith(Configuration.BotPrefix) || message.Message.StartsWith(Action))
            {
                await ChatCommandRepository.HandleCommand(message, session);
                return;
            }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        if (!sessions.TryGetSession(out var receiver, message.Channel) || receiver == null) return;

        if (receiver.Attributes.AwayMessage is not null)
        {
            session.WritePacket(PacketType.ServerChatMessage,
                new BanchoChatMessage
                {
                    Sender = Configuration.BotUsername,
                    Channel = Configuration.BotUsername,
                    Message = $"{receiver.User.Username} is away: {receiver.Attributes.AwayMessage}"
                });
            return;
        }

        if (receiver is { Attributes.IgnoreNonFriendPm: false } ||
            receiver?.User.FriendsList.Contains(session.User.Id) == true)
            receiver.WritePacket(PacketType.ServerChatMessage, message);

        if (receiver != null && receiver.User.SilencedUntil > DateTime.UtcNow)
            session.WritePacket(PacketType.ServerChatPmTargetSilenced,
                new BanchoChatMessage
                {
                    Channel = receiver.User.Username
                });
    }
}