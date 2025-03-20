using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("markscoreasdeleted", requiredPrivileges: UserPrivilege.Developer)]
public class MarkScoreAsDeletedCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 1)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}markscoreasdeleted <id>; Example: {Configuration.BotPrefix}markscoreasdeleted 1");
            return Task.CompletedTask;
        }

        if (!int.TryParse(args[0], out var scoreId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid score id.");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<MarkScoreAsDeletedCommand>(
            () =>
                DeleteScore(session.UserId, scoreId, CancellationToken.None),
            message => ChatCommandRepository.SendMessage(session, message));

        return Task.CompletedTask;
    }

    public async Task DeleteScore(int userId, int requestedScoreId, CancellationToken ct)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<MarkScoreAsDeletedCommand>(
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
                    ChatCommandRepository.TrySendMessage(userId, $"Error message: {deletedScoreResult.Error}");
                    return;
                }

                ChatCommandRepository.TrySendMessage(userId, $"Score {requestedScoreId} has been marked as deleted. Don't forget to update user's stats if needed!");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message));
    }
}