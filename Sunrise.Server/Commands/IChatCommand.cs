using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Commands;

public interface IChatCommand
{
    Task Handle(Session session, ChatChannel? channel, string[]? args);
}