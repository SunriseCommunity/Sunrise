using HOPEless.osu;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects.ChatCommands.Multiplayer;

[ChatCommand("team", "mp", isGlobal: true)]
public class MultiTeamCommand : IChatCommand
{
    public Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        if (channel == null || session.Match == null)
        {
            throw new InvalidOperationException("Multiplayer command was called without being in a multiplayer room.");
        }

        if (session.Match.HasHostPrivileges(session) == false)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used by the host of the room.");
            return Task.CompletedTask;
        }

        if (args == null || args.Length < 2)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp team <username> <color>");
            return Task.CompletedTask;
        }

        if (args[1] != "red" && args[1] != "blue")
        {
            session.SendChannelMessage(channel.Name, "Invalid team color. Use 'red' or 'blue'.");
            return Task.CompletedTask;
        }

        if (session.Match.Match.MultiTeamType != MultiTeamTypes.TeamVs && session.Match.Match.MultiTeamType != MultiTeamTypes.TagTeamVs)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used in team vs or tag team vs mode.");
            return Task.CompletedTask;
        }

        var targetSession = session.Match.Players.Values.FirstOrDefault(x => x.User.Username == args[0]);

        if (targetSession == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return Task.CompletedTask;
        }

        session.Match.ChangeTeam(targetSession, args[1] == "red" ? SlotTeams.Red : SlotTeams.Blue, true);

        session.SendChannelMessage(channel.Name, $"{args[1]}'s team color has been updated.");

        return Task.CompletedTask;
    }
}