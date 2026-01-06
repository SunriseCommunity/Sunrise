using System.Collections.Concurrent;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Objects;

public class ChatChannel(string name, string description, bool isPublic = true, bool isAbstract = false)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool IsPublic { get; } = isPublic;
    public bool IsAbstract { get; } = isAbstract;
    private ConcurrentDictionary<long, byte> UserIds { get; } = new();

    public void AddUser(long userId)
    {
        UserIds.TryAdd(userId, 0);
    }

    public void RemoveUser(long userId)
    {
        UserIds.TryRemove(userId, out _);

        if (UserIds.IsEmpty && IsAbstract)
            ServicesProviderHolder.GetRequiredService<ChatChannelRepository>().RemoveAbstractChannel(Name);
    }

    public void SendToChannel(string message, User? senderUser = null)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var session in UserIds.Keys.Select(userId => sessions.GetSession(userId: userId)))
        {
            if (session?.UserId == senderUser?.Id)
                continue;

            session?.SendChannelMessage(IsAbstract ? Name.Split('_')[0] : Name, message, senderUser);
        }
    }

    public int UsersCount()
    {
        return UserIds.Count;
    }
}