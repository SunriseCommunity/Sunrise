using Sunrise.API.Enums;
using Sunrise.API.Objects;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.Server.Commands.ChatCommands.Bat;

[ChatCommand("setbeatmapsetstatus", requiredPrivileges: UserPrivilege.Bat)]
public class SetBeatmapSetStatusCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}setbeatmapsetstatus <beatmapSetId> <beatmapStatus | \"reset\">; Example: {Configuration.BotPrefix}setbeatmapstatus 13 Ranked"
                + $"\nPossible beatmapStatus options: {string.Join(", ", Enum.GetNames(typeof(BeatmapStatus)))}");
            return;
        }

        if (!int.TryParse(args[0], out var beatmapSetId))
        {
            ChatCommandRepository.SendMessage(session, "Invalid beatmap set id");
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSet = await beatmapService.GetBeatmapSet(session, beatmapSetId);

        if (beatmapSet == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap set not found.");
            return;
        }

        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var batUser = await database.Users.GetUser(session.UserId);

        if (batUser == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }
        
        var webSocketManager = scope.ServiceProvider.GetRequiredService<WebSocketManager>();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<SessionRepository>();

        foreach (var beatmap in beatmapSet.Beatmaps)
        {
            var oldStatus = beatmap.Status;

            var customStatus = await database.CustomBeatmapStatuses.GetCustomBeatmapStatus(beatmap.Checksum!);

            if (args[1] is "reset")
            {

                if (customStatus == null)
                {
                    ChatCommandRepository.SendMessage(session, $"Can't reset beatmap {beatmap.GetBeatmapInGameChatString(beatmapSet)} beatmap status, because it doesn't have any custom beatmap statuses.");
                    continue;
                }

                await database.CustomBeatmapStatuses.DeleteCustomBeatmapStatus(customStatus);
                ChatCommandRepository.SendMessage(session, $"Beatmap {beatmap.GetBeatmapInGameChatString(beatmapSet)} status was updated to default status!");
                continue;
            }

            if (!Enum.TryParse(args[1], out BeatmapStatus status))
            {
                ChatCommandRepository.SendMessage(session, "Invalid status.");
                return;
            }

            if (customStatus != null)
            {
                customStatus.Status = status;
                customStatus.UpdatedByUserId = session.UserId;

                await database.CustomBeatmapStatuses.UpdateCustomBeatmapStatus(customStatus);
            }
            else
            {
                customStatus = new CustomBeatmapStatus
                {
                    Status = status,
                    UpdatedByUserId = session.UserId,
                    BeatmapHash = beatmap.Checksum!,
                    BeatmapSetId = beatmapSet.Id
                };

                await database.CustomBeatmapStatuses.AddCustomBeatmapStatus(customStatus);
            }

            beatmapSet.UpdateBeatmapRanking([customStatus]);

            
            if (oldStatus != status)
                webSocketManager.BroadcastJsonAsync(new WebSocketMessage(WebSocketEventType.CustomBeatmapStatusChanged, new CustomBeatmapStatusChangeResponse(new BeatmapResponse(session, beatmap, beatmapSet), status, oldStatus, new UserResponse(database, sessionRepository, batUser))));

            ChatCommandRepository.SendMessage(session, $"Beatmap {beatmap.GetBeatmapInGameChatString(beatmapSet)} status was updated to {status} from {oldStatus}!");
        }


    }
}