using System.Collections.Concurrent;
using Sunrise.Objects;

namespace Sunrise.Services;

public class PlayerRepository
{
    private ConcurrentDictionary<int, Player> _players = new ConcurrentDictionary<int, Player>();

    public void Add(Player player)
    {
        player.Token = Guid.NewGuid().ToString();
        var result = _players.TryAdd(player.Id, player);

        if (!result)
            throw new Exception("Something went wrong (Possibly player already exists in the dictionary).");
    }

    public void RemovePlayer(int id)
    {
        var result = _players.TryRemove(id, out _);

        if (!result)
            throw new Exception("Failed to remove a player.");
    }
    
    public void RemovePlayer(string token)
    {
        var player = GetPlayer(token);

        var result = _players.TryRemove(player.Id, out _);

        if (player == null || !result)
            throw new Exception("Failed to remove a player.");
    }

    public Player GetPlayer(int id)
    {
        var result = _players.TryGetValue(id, out var player);

        if (!result || player is null)
            throw new Exception("No player with that Id could be found.");

        return player!;
    }
    
    public Player GetPlayerByUsername(string username)
    {
        var player = _players.FirstOrDefault(x => x.Value.Username == username).Value;

        if (player == null)
            throw new Exception("No player with that Token could be found.");

        return player;
    }
    
    
    public Player GetPlayer(string token)
    {
        var player = _players.FirstOrDefault(x => x.Value.Token == token).Value;

        if (player == null)
            throw new Exception("No player with that Token could be found.");

        return player;
    }

    public bool ContainsPlayer(int id)
    {
        return _players.ContainsKey(id);
    }

    public IEnumerable<Player> GetAllPlayers()
    {
        return _players.Values;
    }

    public Player GetPlayerDataFromDb(string token, int id)
    {
        throw new NotImplementedException();
    }
}