namespace Sunrise.Shared.Types.Interfaces;

public interface IChatChannel
{
    string Name { get; }
    string Description { get; }
    bool IsPublic { get; }
    bool IsAbstract { get; }

    void AddUser(int userId);
    void RemoveUser(int userId);
    void SendToChannel(string message, string? sender = null);
    int UsersCount();
}