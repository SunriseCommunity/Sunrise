using Sunrise.Enums;
using Sunrise.Objects;
using Sunrise.Types.Classes;
using Sunrise.Types.Objects;

namespace Sunrise.Database;

public class Players
{
    private readonly Sqlite.SqliteDatabase _sqlite;

    public Players(Sqlite.SqliteDatabase sqlite)
    {
        _sqlite = sqlite;
    }



    public bool CreatePlayer(PlayerObject? user)
    {

        Player player = user.GetPlayer();

        var command = _sqlite.CreateCommand("INSERT INTO players (username, passhash, token, country, privilege, accuracy, total_score, ranked_score, play_count, performance_points, play_time) VALUES (@username, @passhash, @token, @country, @privilege, @accuracy, @total_score, @ranked_score, @play_count, @performance_points, @play_time)");
        command.Parameters.AddWithValue("@username", player.Username);
        command.Parameters.AddWithValue("@passhash", player.HashedPassword);
        command.Parameters.AddWithValue("@token", player.Token);
        command.Parameters.AddWithValue("@country", player.Country);
        command.Parameters.AddWithValue("@privilege", (int)player.Privilege);
        command.Parameters.AddWithValue("@accuracy", player.Accuracy);
        command.Parameters.AddWithValue("@total_score", player.TotalScore);
        command.Parameters.AddWithValue("@ranked_score", player.RankedScore);
        command.Parameters.AddWithValue("@play_count", player.PlayCount);
        command.Parameters.AddWithValue("@performance_points", player.PerformancePoints);
        command.Parameters.AddWithValue("@play_time", 0);

        Console.WriteLine(command.CommandText);

        return _sqlite.ExecuteNonQuery(command) > 0;
    }


    public PlayerObject GetPlayer(string? token, string? username)
    {
        var command = _sqlite.CreateCommand("SELECT * FROM players WHERE username = @username OR token = @token");
        command.Parameters.AddWithValue("@token", token ?? "");
        command.Parameters.AddWithValue("@username", username ?? "");

        Console.WriteLine(command.CommandText);
        Console.WriteLine($"Token: {token}");
        Console.WriteLine($"Username: {username}");

        using var reader = _sqlite.ExecuteReader(command);

        if (reader.Read())
        {
            var player = new Player(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt16(4),
                reader.GetInt16(5),
                (UserPrivileges)reader.GetInt32(6)
            )
            {
                Id = reader.GetInt32(0),
                Token = reader.GetString(3),
                Accuracy = reader.GetFloat(7),
                TotalScore = reader.GetInt64(8),
                RankedScore = reader.GetInt64(9),
                PlayCount = reader.GetInt32(10),
                PerformancePoints = reader.GetInt32(11),
            };

            return new PlayerObject(player);
        }

        return null;
    }

    public bool RemovePlayer(string token)
    {
        var command = _sqlite.CreateCommand("DELETE FROM players WHERE token = @token");
        command.Parameters.AddWithValue("@token", token);

        return _sqlite.ExecuteNonQuery(command) > 0;
    }

    public void UpdateToken(string username, string newToken)
    {
        var command = _sqlite.CreateCommand("UPDATE players SET token = @newToken WHERE username = @username");
        command.Parameters.AddWithValue("@newToken", newToken);
        command.Parameters.AddWithValue("@username", username);

        _sqlite.ExecuteNonQuery(command);
    }

}