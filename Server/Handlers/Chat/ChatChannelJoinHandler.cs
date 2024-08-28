using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Chat;

[PacketHandler(PacketType.ClientChatChannelJoin)]
public class ChatChannelJoinHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var channelName = new BanchoString(packet.Data);

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChannelRepository>();

        if (channelName.Value.StartsWith('#')) chatChannels.JoinChannel(channelName.Value, session);

        return Task.CompletedTask;
    }
}