using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.System;

[ChatCommand("calculationrun", requiredPrivileges: UserPrivilege.SuperUser)]
public class CalculationRunCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        var action = args?.FirstOrDefault();

        if (action is not ("status" or "start" or "stop"))
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}calculationrun <status | start | stop>. start recalculates every score even if the rosu version didn't change.");
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var run = await database.Calculations.GetLastRun();
        var unfinishedRun = run is { FinishedAt: null } ? run : null;

        switch (action)
        {
            case "start":
            {
                var targetResult = await scope.ServiceProvider.GetRequiredService<CalculatorService>().GetTargetRosuVersion(BaseSession.GenerateServerSession());
                if (targetResult.IsFailure)
                {
                    ChatCommandRepository.SendMessage(session, $"Couldn't get calculator version: {targetResult.Error.Message}");
                    return;
                }

                var targetVersionId = await database.Calculations.GetOrCreateVersionId(targetResult.Value);
                var newRun = await database.Calculations.StartRun(targetVersionId!.Value, isForced: true);

                ChatCommandRepository.SendMessage(session,
                    $"Started forced calculation run {newRun.Id} for rosu {targetResult.Value}{(unfinishedRun != null ? $", superseded run {unfinishedRun.Id}" : "")}. Scores are enqueued on the next minute tick.");
                return;
            }
            case "stop":
            {
                if (unfinishedRun == null)
                {
                    ChatCommandRepository.SendMessage(session, "There is no active calculation run.");
                    return;
                }

                await database.Calculations.StopRun(unfinishedRun);

                ChatCommandRepository.SendMessage(session,
                    $"Stopped calculation run {unfinishedRun.Id} at phase {unfinishedRun.Phase}. Pending tasks were deleted and the freeze is lifted; user stats and leaderboards were not rebuilt.");
                return;
            }
        }

        if (run == null)
        {
            ChatCommandRepository.SendMessage(session, "No calculation run has been started yet.");
            return;
        }

        var rosuVersion = await database.DbContext.CalculationVersions.NotCacheable()
            .Where(v => v.Id == run.TargetVersionId)
            .Select(v => v.RosuVersion)
            .FirstAsync();

        var tasks = await database.Calculations.CountRunTasks(run);
        var state = run.FinishedAt == null ? "active (stats and leaderboards frozen)"
            : run.IsSuperseded ? $"superseded at {run.FinishedAt:u}"
            : run.Phase == CalculationRunPhase.Finished ? $"finished at {run.FinishedAt:u}"
            : $"stopped at {run.FinishedAt:u}";

        ChatCommandRepository.SendMessage(session,
            $"Run {run.Id}{(run.IsForced ? " (forced)" : "")}: rosu {rosuVersion}, phase {run.Phase}, {state}, started {run.StartedAt:u}. " +
            $"Tasks: pending {tasks.GetValueOrDefault(ScoreProcessingStatus.Pending)}, processing {tasks.GetValueOrDefault(ScoreProcessingStatus.Processing)}, failed {tasks.GetValueOrDefault(ScoreProcessingStatus.Failed)}.");
    }
}
