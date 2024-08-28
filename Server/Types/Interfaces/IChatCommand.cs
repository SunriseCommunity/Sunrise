using Sunrise.Server.Chat;
using Sunrise.Server.Objects;

namespace Sunrise.Server.Types.Interfaces;

public interface IChatCommand
{
    Task Handle(Session session, ChatChannel? channel, string[]? args);
}