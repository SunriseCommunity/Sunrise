using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands;

public interface IChatCommand
{
    Task Handle(Session session, ChatChannel? channel, string[]? args);
}