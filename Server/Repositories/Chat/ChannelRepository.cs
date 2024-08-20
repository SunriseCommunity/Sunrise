using System.Collections.Concurrent;
using HOPEless.Bancho;
using osu.Shared;
using Sunrise.Server.Objects;

namespace Sunrise.Server.Repositories.Chat;

public class ChannelRepository
{
    private readonly ConcurrentDictionary<string, ChatChannel> _channels = new()
    {
        ["#osu"] = new ChatChannel("#osu", "General chat channel."),
        ["#announce"] = new ChatChannel("#announce", "Announcement chat channel."),
        ["#staff"] = new ChatChannel("#staff", "Staff chat channel.", false),
        ["#userlog"] = new ChatChannel("#userlog", "Your session logs."),
        ["#AYAYA"] = new ChatChannel("#AYAYA", "Feel free to spam AYAYA here.")
    };

    public void JoinChannel(string name, Session session, bool abstractChannel = false)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            if (abstractChannel == false)
            {
                return;
            }

            channel = CreateChannel(name);
        }

        session.SendJoinChannel(channel);
        channel.AddUser(session.User.Id);
    }

    public void LeaveChannel(string name, Session session, bool abstractChannel = false)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            return;
        }

        if (abstractChannel) session.WritePacket(PacketType.ServerChatChannelRevoked, channel.Name);

        channel.RemoveUser(session.User.Id);
    }

    private ChatChannel CreateChannel(string name)
    {
        var channel = name switch
        {
            not null when name.StartsWith("#spectator_") => new ChatChannel(name, "Spectator chat channel.", false),
            not null when name.StartsWith("#multiplayer_") => new ChatChannel(name, "Multiplayer chat channel.", false),
            _ => throw new InvalidOperationException("Invalid channel name.")
        };

        _channels[name] = channel;

        return channel;
    }

    public ChatChannel? GetChannel(string name)
    {
        return _channels.GetValueOrDefault(name);
    }

    public List<ChatChannel> GetChannels(Session? session = null)
    {
        if (session == null)
        {
            return _channels.Values.Where(x => x.IsPublic).ToList();
        }

        return _channels.Values.Where(x => x.IsPublic || session.User.Privilege >= PlayerRank.SuperMod).ToList();
    }
}