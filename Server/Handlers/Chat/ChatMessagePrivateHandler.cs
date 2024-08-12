using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientChatMessagePrivate)]
public class ChatMessagePrivateHandler : IHandler
{
    private const string Action = "ACTION";

    public async Task Handle(BanchoPacket packet, Session session)
    {
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = session.User.Username,
            SenderId = session.User.Id
        };

        if (message.Channel == Configuration.BotUsername)
        {
            if (message.Message.StartsWith(Configuration.BotPrefix) || message.Message.StartsWith(Action))
            {
                await CommandRepository.HandleCommand(message, session);
                return;
            }
        }

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var receiver = sessions.GetSessionBy(message.Channel);

        if (receiver is null)
        {
            return;
        }

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

        if (receiver is { Attributes.IgnoreNonFriendPm: false } || receiver?.User.FriendsList.Contains(session.User.Id) == true)
        {
            receiver.WritePacket(PacketType.ServerChatMessage, message);
        }

        await receiver?.FetchUser()!;

        if (receiver.User.SilencedUntil > DateTime.UtcNow)
        {
            session.WritePacket(PacketType.ServerChatPmTargetSilenced,
                new BanchoChatMessage
                {
                    Channel = receiver.User.Username
                });
        }
    }
}