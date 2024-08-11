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

    public int UsersCount()
    {
        return UserIds.Count;
    }
}