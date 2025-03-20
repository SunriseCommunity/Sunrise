using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("deletescore", requiredPrivileges: UserPrivilege.Developer)]
public class DeleteScoreCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}deletescore <id>; Example: {Configuration.BotPrefix}deletescore 1");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args[0], out var scoreId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid score id.");
            return Task.CompletedTask;
        }

        BackgroundTasks.TryStartNewBackgroundJob<DeleteScoreCommand>(
            () =>
                DeleteScore(session.UserId, scoreId),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task DeleteScore(int userId, int requestedScoreId)
    {
        await BackgroundTasks.ExecuteBackgroundTask<DeleteScoreCommand>(
            async () =>
            {
                using var scope = ServicesProviderHolder.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

                var score = await database.Scores.GetScore(requestedScoreId);

                if (score == null)
                {
                    ChatCommandRepository.TrySendMessage(userId, $"Score {requestedScoreId} not found.");
                    return;
                }

                var deletedScoreResult = await database.Scores.MarkScoreAsDeleted(score);

                if (deletedScoreResult.IsFailure)
                {
                    ChatCommandRepository.TrySendMessage(userId, $"Failed to mark score {requestedScoreId} as deleted. Please check console for more information.");
                    return;
                }

                ChatCommandRepository.TrySendMessage(userId, $"Score {requestedScoreId} has been deleted. Don't forget to update user's stats if needed!");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}