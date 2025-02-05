using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Chat.Commands.Multiplayer;

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

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapId: beatmapId);

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