using osu.Shared;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat;

public class ChatCommand(IChatCommand handler, string prefix, UserPrivileges requiredPrivileges, bool isGlobal = false)
{
    public UserPrivileges RequiredPrivileges { get; } = requiredPrivileges;
    public bool IsGlobal { get; set; } = isGlobal;
    public string Prefix { get; set; } = prefix;

    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        return handler.Handle(session, channel, args);
    }
}