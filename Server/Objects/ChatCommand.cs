using osu.Shared;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects;

public class ChatCommand(IChatCommand handler, PlayerRank requiredPrivileges, bool isGlobal = false)
{
    public PlayerRank RequiredPrivileges { get; } = requiredPrivileges;
    public bool IsGlobal { get; set; } = isGlobal;

    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        return handler.Handle(session, channel, args);
    }
}