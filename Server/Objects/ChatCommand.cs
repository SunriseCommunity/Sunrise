using osu.Shared;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects;

public class ChatCommand(IChatCommand handler, PlayerRank requiredPrivileges)
{
    public PlayerRank RequiredPrivileges { get; } = requiredPrivileges;

    public Task Handle(Session session, string[]? args)
    {
        return handler.Handle(session, args);
    }
}