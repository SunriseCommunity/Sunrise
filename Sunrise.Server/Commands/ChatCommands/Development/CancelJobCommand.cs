using Hangfire;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("canceljob", requiredPrivileges: UserPrivilege.Developer)]
public class CancelJobCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}canceljob <job_id>; Example: {Configuration.BotPrefix}canceljob 19");
            return Task.CompletedTask;
        }

        var jobId = args[0];

        var isJobDeleted = BackgroundJob.Delete(jobId);

        if (!isJobDeleted)
        {
            ChatCommandRepository.SendMessage(session,
                $"Couldn't find job with id \"{jobId}\"");
            return Task.CompletedTask;
        }

        ChatCommandRepository.SendMessage(session,
            $"Successfully sent cancel signal for \"{jobId}\" job.");

        return Task.CompletedTask;
    }
}