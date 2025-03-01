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
    private List<long> UserIds { get; } = [];

    public void AddUser(long userId)
    {
        UserIds.Add(userId);
    }

    public void RemoveUser(long userId)
    {
        UserIds.Remove(userId);

        if (UserIds.Count == 0 && IsAbstract)
            ServicesProviderHolder.GetRequiredService<ChatChannelRepository>().RemoveAbstractChannel(Name);
    }

    public void SendToChannel(string message,  User? senderUser = null)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        foreach (var session in UserIds.Select(userId => sessions.GetSession(userId: userId)))
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