using System.Collections.Concurrent;
using System.ComponentModel;
using Sunrise.Types.Classes;

namespace Sunrise.Services;

/*
 *  PLEASE JUST FOR LOVE OF PEPPY GOD FORGIVE ME FOR THIS CODE
 *  IT SO BAD I CAN'T EVEN LOOK AT IT
 *  I'M SORRY
 *  I'M SORRY
 *  I'M SORRY
 *
 *  will refactor this next PR. -richard
 */

[Description("This class is responsible for managing the currently connected players.")]
public class PlayersPoolService
{
    private readonly ConcurrentDictionary<int, Player> _players = new ConcurrentDictionary<int, Player>();

    public PlayersPoolService()
    {
        Console.WriteLine("PlayersPoolService has been initialized.");
    }

    public void Add(Player player)
    {
        var result = _players.TryAdd(player.Id, player);

        Console.WriteLine($"Player {player.Username} has been added to the pool.");


        if (!result)
        {

            Console.WriteLine("Something went wrong (Possibly player already exists in the dictionary).");

            RemovePlayer(player.Id);
            _players.TryAdd(player.Id, player);
        }

    }

    public void RemovePlayer(int id = 0, string? token = null)
    {
        if (id == 0 && token == null)
            throw new Exception("No Id or Token was provided.");

        if (token != null)
        {
            var player = GetPlayer(token: token);
            id = player.Id;
        }

        Console.WriteLine($"Player with Id {id} has been removed from the pool.");

        var result = _players.TryRemove(id, out _);

        if (!result)
        {
            Console.WriteLine("Failed to remove a player.");
        }
    }

    public Player GetPlayer(int id = 0, string? username = null, string? token = null)
    {
        if (id == 0 && username == null && token == null)
            throw new Exception("No Id, Username or Token was provided.");

        if (username != null)
        {
            var result = _players.FirstOrDefault(x => x.Value.Username == username).Value;

            if (result == null)
                throw new Exception("No player with that Username could be found.");

            return result;
        }
        else if (token != null)
        {
            var result = _players.FirstOrDefault(x => x.Value.Token == token).Value;

            if (result == null)
                throw new Exception("No player with that Token could be found.");

            return result;
        }
        else
        {
            var result = _players.TryGetValue(id, out var player);

            if (!result || player is null)
                throw new Exception("No player with that Id could be found.");

            return player;
        }
    }

    public bool ContainsPlayer(int id)
    {
        return _players.ContainsKey(id);
    }

    public IEnumerable<Player> GetAllPlayers()
    {
        return _players.Values;
    }
}