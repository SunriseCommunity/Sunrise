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

[PacketHandler(PacketType.ClientChatMessagePrivate)]
public class ChatMessagePrivateHandler : IPacketHandler
{
    private const string Action = "ACTION";
    private readonly ChatRateLimiter _rateLimiter = new(10, TimeSpan.FromSeconds(5));

    public async Task Handle(BanchoPacket packet, Session session)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                
        var user = await database.Users.GetUser(session.UserId);
        if (user == null)
            return;
        
        var message = new BanchoChatMessage(packet.Data)
        {
            Sender = user.Username,
            SenderId = user.Id
        };

        if (!_rateLimiter.CanSend(session)) return;

        if (message.Channel == Configuration.BotUsername)
            if (message.Message.StartsWith(Configuration.BotPrefix) || message.Message.StartsWith(Action))
            {
                await ChatCommandRepository.HandleCommand(message, session);
                return;
            }

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        if (!sessions.TryGetSession(username: message.Channel, null, out var receiver) || receiver == null) return;
        
        var receiverUser = await database.Users.GetUser(receiver.UserId);
        if (receiverUser == null)
            return;

        if (receiver.Attributes.AwayMessage is not null )
        {
            session.WritePacket(PacketType.ServerChatMessage,
                new BanchoChatMessage
                {
                    Sender = Configuration.BotUsername,
                    Channel = Configuration.BotUsername,
                    Message = $"{receiverUser.Username} is away: {receiver.Attributes.AwayMessage}"
                });
            return;
        }

        if (receiver is { Attributes.IgnoreNonFriendPm: false } || receiverUser.FriendsList.Contains(session.UserId))
            receiver.WritePacket(PacketType.ServerChatMessage, message);

        if (receiverUser.SilencedUntil > DateTime.UtcNow)
            session.WritePacket(PacketType.ServerChatPmTargetSilenced,
                new BanchoChatMessage
                {
                    Channel = receiverUser.Username
                });
    }
}