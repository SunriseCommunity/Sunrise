using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("map", "mp", isGlobal: true)]
public class MultiMapCommand : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return;
        }

        if (args == null || args.Length == 0)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp map <beatmapId> [<playMode>]");
            return;
        }

        var currentMatch = session.Match.Match;

        if (int.TryParse(args[0], out var beatmapId) == false)
        {
            session.SendChannelMessage(channel.Name, "Invalid beatmap ID.");
            return;
        }

        using var scope = ServicesProviderHolder.CreateScope();
        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapId: beatmapId);

        if (beatmapSetResult.IsFailure)
        {
            session.SendChannelMessage(channel.Name, beatmapSetResult.Error.Message);
            return;
        }

        var beatmapSet = beatmapSetResult.Value;

        if (beatmapSet == null)
        {
            session.SendChannelMessage(channel.Name, "Beatmap set not found.");
            return;
        }

        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(x => x.Id == beatmapId);

        if (beatmap == null)
        {
            session.SendChannelMessage(channel.Name, "Beatmap not found.");
            return;
        }

        currentMatch.BeatmapId = beatmapId;
        currentMatch.BeatmapChecksum = beatmap.Checksum;
        currentMatch.BeatmapName = $"{beatmapSet.Artist} - {beatmapSet.Title} [{beatmap.Version}]";
        currentMatch.PlayMode = (GameMode)beatmap.ModeInt;

        if (args.Length > 1 && Enum.TryParse(args[1], true, out GameMode playMode))
        {
            currentMatch.PlayMode = playMode;
        }

        session.Match.UpdateMatchSettings(currentMatch, session);
    }
}