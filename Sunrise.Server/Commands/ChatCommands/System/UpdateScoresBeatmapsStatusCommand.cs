using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.System;

[Obsolete("This command is deprecated and will be removed in next versions.")]
[ChatCommand("updatescoresbeatmapstatus", requiredPrivileges: UserPrivilege.SuperUser)]
public class UpdateScoresBeatmapsStatusCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        ChatCommandRepository.SendMessage(
            session,
            $"This command is deprecated and will be removed in next versions. Use {Configuration.BotPrefix}recalculatescore <scoreId> <beatmapStatus> for single-score repairs.");
        return Task.CompletedTask;
    }
}