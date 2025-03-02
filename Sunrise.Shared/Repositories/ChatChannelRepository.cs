using System.Collections.Concurrent;
using HOPEless.Bancho;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Repositories;

public class ChatChannelRepository
{
    private readonly ConcurrentDictionary<string, ChatChannel> _channels = new()
    {
        ["#osu"] = new ChatChannel("#osu", "General chat channel."),
        ["#announce"] = new ChatChannel("#announce", "Announcement chat channel."),
        ["#lobby"] = new ChatChannel("#lobby", "Multiplayer lobby channel."),
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

            channel = CreateAbstractChannel(name);
        }

        session.SendJoinChannel(channel);
        channel.AddUser(session.UserId);
    }

    public void LeaveChannel(string name, Session session, bool abstractChannel = false)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            return;
        }

        if (abstractChannel) session.WritePacket(PacketType.ServerChatChannelRevoked, channel.Name);

        channel.RemoveUser(session.UserId);
    }

    public void RemoveAbstractChannel(string name)
    {
        if (_channels.TryGetValue(name, out var channel) && channel.IsAbstract)
        {
            _channels.TryRemove(name, out _);
        }
    }

    private ChatChannel CreateAbstractChannel(string name)
    {
        var channel = name switch
        {
            not null when name.StartsWith("#spectator_") => new ChatChannel("#spectator", "Spectator chat channel.", false, true),
            not null when name.StartsWith("#multiplayer_") => new ChatChannel("#multiplayer", "Multiplayer chat channel.", false, true),
            _ => throw new InvalidOperationException("Invalid channel name.")
        };

        _channels[name] = channel;

        return channel;
    }

    public ChatChannel? GetChannel(Session session, string name)
    {
        var channel = name switch
        {
            not null when name == "#spectator" => GetChannel(session, $"#spectator_{session.Spectating?.UserId}"),
            not null when name == "#multiplayer" => GetChannel(session, $"#multiplayer_{session.Match?.Match.MatchId}"),
            _ => _channels!.GetValueOrDefault(name)
        };

        return channel;
    }

    public List<ChatChannel> GetChannels(Session? session = null)
    {
        if (session == null)
        {
            return _channels.Values.Where(x => x.IsPublic).ToList();
        }
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var sessionPrivilege = UserPrivilege.User;

        var user = database.Users.GetUser(id: session.UserId, options: new QueryOptions(true)).ConfigureAwait(false).GetAwaiter().GetResult();
        if (user != null)
            sessionPrivilege = user.Privilege;

        return _channels.Values.Where(x => x.IsPublic || sessionPrivilege.HasFlag(UserPrivilege.Admin)).ToList();
    }
}