using HOPEless.Bancho.Objects;
using Sunrise.Database;
using Sunrise.Types.Classes;

namespace Sunrise.Types.Objects;

public class PlayerObject
{
    public Player Player { get; private set; }

    public PlayerObject(UserSchema user)
    {
        Player = new Player(user);
    }

    public Player GetPlayer()
    {
        return Player;
    }

    public void SetPlayerStatus(BanchoUserStatus status)
    {
        Player.PlayerStatus = status;
    }

    public byte[] GetPacketBytes()
    {
        return Player.Queue.GetBytesToSend();
    }
}