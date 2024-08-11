using Sunrise.Server.Objects;

namespace Sunrise.Server.Types.Interfaces;

public interface IChatCommand
{
    Task Handle(Session session, string[]? args);
}