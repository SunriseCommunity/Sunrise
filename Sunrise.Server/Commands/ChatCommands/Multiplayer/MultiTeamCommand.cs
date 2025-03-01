using HOPEless.osu;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.Multiplayer;

[ChatCommand("team", "mp", isGlobal: true)]
public class MultiTeamCommand : IChatCommand
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

        if (args == null || args.Length < 2)
        {
            session.SendChannelMessage(channel.Name, "Usage: !mp team <username> <color>");
            return;
        }

        if (args[1] != "red" && args[1] != "blue")
        {
            session.SendChannelMessage(channel.Name, "Invalid team color. Use 'red' or 'blue'.");
            return;
        }

        if (session.Match.Match.MultiTeamType != MultiTeamTypes.TeamVs && session.Match.Match.MultiTeamType != MultiTeamTypes.TagTeamVs)
        {
            session.SendChannelMessage(channel.Name, "This command can only be used in team vs or tag team vs mode.");
            return;
        }
        
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                
        var targetUser = await database.Users.GetUser(username: args[0]);
        if (targetUser == null)
            return;

        var targetSession = session.Match.Players.Values.FirstOrDefault(x => x.UserId == targetUser.Id);

        if (targetSession == null)
        {
            session.SendChannelMessage(channel.Name, "User not found.");
            return;
        }

        session.Match.ChangeTeam(targetSession, args[1] == "red" ? SlotTeams.Red : SlotTeams.Blue, true);

        session.SendChannelMessage(channel.Name, $"{args[1]}'s team color has been updated.");
    }
}