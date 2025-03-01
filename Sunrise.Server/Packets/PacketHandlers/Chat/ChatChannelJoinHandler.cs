using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers.Chat;

[PacketHandler(PacketType.ClientChatChannelJoin)]
public class ChatChannelJoinHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var channelName = new BanchoString(packet.Data);

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();

        if (channelName.Value.StartsWith('#')) chatChannels.JoinChannel(channelName.Value, session);

        return Task.CompletedTask;
    }
}