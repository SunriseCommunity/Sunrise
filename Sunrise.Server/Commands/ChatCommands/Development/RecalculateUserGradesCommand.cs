using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Server.Commands.ChatCommands.Development;

[ChatCommand("recalculateusergrades", requiredPrivileges: UserPrivilege.Developer)]
public class RecalculateUserGradesCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}recalculateusergrades <modeEnum | all> <isStartMaintenance> | Example: {Configuration.BotPrefix}recalculateusergrades 0 true for osu std calculation with maintenance mode on.");
            return Task.CompletedTask;
        }

        GameMode? mode = Enum.TryParse(args[0], out GameMode parsedMode) ? parsedMode : null;

        if (mode == null && args[0] != "all")
        {
            ChatCommandRepository.SendMessage(session, "Invalid mode.");
            return Task.CompletedTask;
        }

        if (!bool.TryParse(args[1], out var isStartMaintenance))
        {
            ChatCommandRepository.SendMessage(session, "Invalid isStartMaintenance value.");
            return Task.CompletedTask;
        }

        BackgroundTaskService.TryStartNewBackgroundJob<RecalculateUserGradesCommand>(
            () => RecalculateUserGrades(session.UserId, CancellationToken.None, mode),
            message => ChatCommandRepository.TrySendMessage(session.UserId, message),
            isStartMaintenance);

        return Task.CompletedTask;
    }

    public async Task RecalculateUserGrades(int userId, CancellationToken token, GameMode? mode = null)
    {
        await BackgroundTaskService.ExecuteBackgroundTask<RecalculateUserGradesCommand>(
            async () =>
            {
                if (!mode.HasValue)
                {
                    foreach (var gameMode in Enum.GetValues<GameMode>())
                    {
                        await RecalculateUserGradesInGamemode(userId, token, gameMode);
                    }
                }
                else
                {
                    await RecalculateUserGradesInGamemode(userId, token, mode.Value);
                }

                ChatCommandRepository.TrySendMessage(userId, "Recalculation user grades has finished");
            },
            message => ChatCommandRepository.TrySendMessage(userId, message)
        );
    }

    public async Task RecalculateUserGradesInGamemode(int userId, CancellationToken token, GameMode mode)
    {
        var startTime = DateTime.UtcNow;

        var pageSize = 50;

        for (var x = 1;; x++)
        {
            using var scope = ServicesProviderHolder.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

            var pageUsers = await database.Users.GetUsers(options: new QueryOptions(new Pagination(x, pageSize)));

            foreach (var user in pageUsers)
            {
                await UpdateUserGrades(database, user.Id, mode, userId);
                token.ThrowIfCancellationRequested();
            }

            if (pageUsers.Count < pageSize) break;
        }

        ChatCommandRepository.TrySendMessage(userId,
            $"Recalculated user grades for mode {mode}. Took {(DateTime.UtcNow - startTime).TotalSeconds} seconds.");
    }

    private async Task UpdateUserGrades(DatabaseService database, int userId, GameMode mode, int userIdToSendUpdatesTo)
    {
        var userGrades = await database.Users.Grades.GetUserGrades(userId, mode);
        if (userGrades == null)
            throw new Exception($"User {userId} has no userGrades");
        
        userGrades = ClearUserGrades(userGrades);

        var pageSize = 100;

        for (var i = 1;; i++)
        {
            var (pageScores, totalScores) = await database.Scores.GetUserScores(userId, mode, ScoreTableType.Best, new QueryOptions(new Pagination(i, pageSize)));

            if (totalScores == 0)
            {
                return;
            }

            foreach (var score in pageScores)
            {
                userGrades.UpdateWithScore(score);
            }

            if (pageScores.Count < pageSize) break;
        }

        var updateUserGradesResult = await database.Users.Grades.UpdateUserGrades(userGrades);
        if (updateUserGradesResult.IsFailure)
            throw new Exception(updateUserGradesResult.Error);
    }

    private UserGrades ClearUserGrades(UserGrades grades)
    {
        grades.CountXH = 0;
        grades.CountX = 0;
        grades.CountSH = 0;
        grades.CountS = 0;
        grades.CountA = 0;
        grades.CountB = 0;
        grades.CountC = 0;
        grades.CountD = 0;

        return grades;
    }
}