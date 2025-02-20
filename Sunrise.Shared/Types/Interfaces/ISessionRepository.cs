using HOPEless.Bancho;
using Microsoft.AspNetCore.Identity.Data;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Shared.Types.Interfaces;

public interface ISessionRepository
{
    void WriteToAllSessions(PacketType type, object data, int ignoreUserId = -1);

    ISession CreateSession(User user, Location location, ILoginRequest loginRequest);

    void SoftRemoveSession(ISession session);

    void RemoveSession(ISession session);

    bool TryGetSession(string username, string passhash, out ISession? session);

    bool TryGetSession(out ISession? session, string? username = null, string? token = null, int? userId = null);

    ISession? GetSession(string? username = null, string? token = null, int? userId = null);

    bool IsUserOnline(int userId);

    Task SendCurrentPlayers(ISession session);

    List<ISession> GetSessions();

    void ClearInactiveSessions();
}