using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands;

public class ChatCommand(IChatCommand handler, string prefix, UserPrivilege requiredPrivileges, bool isGlobal = false)
{
    public UserPrivilege RequiredPrivileges { get; } = requiredPrivileges;
    public bool IsGlobal { get; set; } = isGlobal;
    public string Prefix { get; set; } = prefix;

    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        return handler.Handle(session, channel, args);
    }
}