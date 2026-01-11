using Sunrise.Shared.Attributes;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands;

public class ChatCommand(IChatCommand handler, string prefix, UserPrivilege requiredPrivileges, bool isGlobal = false, bool isHidden = false)
{
    public UserPrivilege RequiredPrivileges { get; } = requiredPrivileges;
    public bool IsGlobal { get; set; } = isGlobal;
    public bool IsHidden { get; set; } = isHidden;
    public string Prefix { get; set; } = prefix;

    [TraceExecution]
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        return handler.Handle(session, channel, args);
    }
}