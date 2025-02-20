using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Types.Interfaces;

namespace Sunrise.Server.Chat;

public class ChatChannel(string name, string description, bool isPublic = true, bool isAbstract = false) : IChatChannel
{
    private List<int> UserIds { get; } = [];
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool IsPublic { get; } = isPublic;
    public bool IsAbstract { get; } = isAbstract;

    public void AddUser(int userId)
    {
        UserIds.Add(userId);
    }

    public void RemoveUser(int userId)
    {
        UserIds.Remove(userId);

        if (UserIds.Count == 0 && IsAbstract)
            ServicesProviderHolder.GetRequiredService<ChannelRepository>().RemoveAbstractChannel(Name);
    }

    public void SendToChannel(string message, string? sender = null)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<ISessionRepository>();

        foreach (var session in UserIds.Select(userId => sessions.GetSession(userId: userId)))
        {
            if (session?.User.Username == sender)
                continue;

            session?.SendChannelMessage(IsAbstract ? Name.Split('_')[0] : Name, message, sender);
        }
    }

    public int UsersCount()
    {
        return UserIds.Count;
    }
}