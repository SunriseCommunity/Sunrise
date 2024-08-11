using Sunrise.Server.Repositories;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects;

public class ChatChannel(string name, string description, bool isPublic = true)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool IsPublic { get; } = isPublic;
    private List<int> UserIds { get; } = [];

    public void AddUser(int userId)
    {
        UserIds.Add(userId);
    }

    public void RemoveUser(int userId)
    {
        UserIds.Remove(userId);
    }

    public void SendToChannel(string message)
    {
        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        foreach (var session in UserIds.Select(userId => sessions.GetSessionBy(userId)))
        {
            session?.SendChannelMessage(Name, message);
        }
    }

    public int UsersCount()
    {
        return UserIds.Count;
    }
}