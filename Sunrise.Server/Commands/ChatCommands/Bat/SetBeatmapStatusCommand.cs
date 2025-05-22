using Sunrise.API.Enums;
using Sunrise.API.Objects;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Beatmap;
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

[ChatCommand("setbeatmapstatus", requiredPrivileges: UserPrivilege.Bat)]
public class SetBeatmapStatusCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (args == null || args.Length < 2)
        {
            ChatCommandRepository.SendMessage(session,
                $"Usage: {Configuration.BotPrefix}setbeatmapstatus <beatmapId | beatmapHash> <beatmapStatus | \"reset\">; Example: {Configuration.BotPrefix}setbeatmapstatus 13 Ranked"
                + $"\nPossible beatmapStatus options: {string.Join(", ", Enum.GetNames(typeof(BeatmapStatusWeb)))}");
            return;
        }
        
        string? beatmapHash = null;
        var isIdProvided = int.TryParse(args[0], out var beatmapId);

        if (!isIdProvided)
            beatmapHash = args[0];

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var batUser = await database.Users.GetUser(session.UserId);

        if (batUser == null)
        {
            ChatCommandRepository.SendMessage(session, "User not found.");
            return;
        }

        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapHash: beatmapHash, beatmapId: beatmapId);

        if (beatmapSetResult.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, beatmapSetResult.Error.Message);
            return;
        }

        var beatmapSet = beatmapSetResult.Value;

        if (beatmapSet == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap set not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Checksum == beatmapHash || x.Id == beatmapId);

        if (beatmap == null)
        {
            ChatCommandRepository.SendMessage(session, "Beatmap not found.");
            return;
        }

        var resetBeatmapStatus = args[1] is "reset";
        var isParsedCustomStatus = Enum.TryParse(args[1], out BeatmapStatusWeb status);

        if (!isParsedCustomStatus && !resetBeatmapStatus)
        {
            ChatCommandRepository.SendMessage(session, "Invalid beatmap set status.");
            return;
        }

        if (args.Length < 3 || args[2] != "-y")
        {
            ChatCommandRepository.SendMessage(session,
                $"You are going to update {beatmap.GetBeatmapInGameChatString(beatmapSet)} " +
                $"{(resetBeatmapStatus ? "to their default status." : $"to {status}.")}\n"
                + $"Use same command with \"-y\" flag to continue. Example: \"{Configuration.BotPrefix}setbeatmapstatus 13 Ranked -y\"");
            return;
        }
        
        var oldStatus = beatmap.StatusGeneric;

        var changeBeatmapSetStatusResult = await beatmapService.ChangeBeatmapCustomStatus(
            batUser,
            beatmap,
            resetBeatmapStatus ? null : status,
            resetBeatmapStatus ? true : null
        );

        if (changeBeatmapSetStatusResult.IsFailure)
        {
            ChatCommandRepository.SendMessage(session, changeBeatmapSetStatusResult.Error);
            return;
        }

        if (resetBeatmapStatus)
        {
            ChatCommandRepository.SendMessage(session, $"Beatmap {beatmap.GetBeatmapInGameChatString(beatmapSet)} status was updated to default status");
            return;
        }

        var customStatus = changeBeatmapSetStatusResult.Value;

        if (customStatus == null)
        {
            ChatCommandRepository.SendMessage(session, "Couldn't get custom status.");
            return;
        }

        beatmapSet.UpdateBeatmapRanking([customStatus]);
        
        var webSocketManager = scope.ServiceProvider.GetRequiredService<WebSocketManager>();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<SessionRepository>();

        if (oldStatus != status)
            webSocketManager.BroadcastJsonAsync(new WebSocketMessage(WebSocketEventType.CustomBeatmapStatusChanged, new CustomBeatmapStatusChangeResponse(new BeatmapResponse(sessionRepository, beatmap, beatmapSet), status, oldStatus, new UserResponse(sessionRepository, batUser))));

        ChatCommandRepository.SendMessage(session, $"Beatmap {beatmap.GetBeatmapInGameChatString(beatmapSet)} status was updated to {status} from {oldStatus}!");
    }
}