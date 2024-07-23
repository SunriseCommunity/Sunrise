using Sunrise.Enums;
using Sunrise.Objects;
using Sunrise.Types.Objects;

namespace Sunrise.Database;

public class Scores
{
    private readonly Sqlite.SqliteDatabase _sqlite;

    public Scores(Sqlite.SqliteDatabase sqlite)
    {
        _sqlite = sqlite;
    }

    public void AddScore(ScoreObject score)
    {
        var scoreCheckSum = score.Score.BeatmapHash;

        var command = _sqlite.CreateCommand("INSERT INTO scores (user_name, beatmap_id, beatmap_hash, score, max_combo, accuracy, mods, rank, time, play_mode, score_checksum, replay) VALUES (@user_name, @beatmap_id, @beatmap_hash, @score, @max_combo, @accuracy, @mods, @rank, @time, @play_mode, @score_checksum, @replay)");

        command.Parameters.AddWithValue("@user_name", score.Score.Username);
        command.Parameters.AddWithValue("@beatmap_id", score.Score.BeatmapHash);
        command.Parameters.AddWithValue("@beatmap_hash", score.Score.BeatmapHash);
        command.Parameters.AddWithValue("@score", score.Score.TotalScore);
        command.Parameters.AddWithValue("@max_combo", score.Score.MaxCombo);
        command.Parameters.AddWithValue("@accuracy", "0");
        command.Parameters.AddWithValue("@mods", score.Score.Mods);
        command.Parameters.AddWithValue("@rank", score.Score.Rank); // @Deprecated Will be set by the server
        command.Parameters.AddWithValue("@time", score.Score.WhenPlayed.ToString("yyMMddHHmmss"));
        command.Parameters.AddWithValue("@play_mode", score.Score.PlayMode);
        command.Parameters.AddWithValue("@score_checksum", score.Score.GetHashCode());
        command.Parameters.AddWithValue("@replay", "0");

        _sqlite.ExecuteNonQuery(command);
    }

    public List<Score> GetScores(string hash)
    {
        var command = _sqlite.CreateCommand("SELECT * FROM scores WHERE beatmap_hash = @beatmap_hash");
        command.Parameters.AddWithValue("@beatmap_hash", hash);

        using var reader = _sqlite.ExecuteReader(command);
        List<Score> scores = new();

        while (reader.Read())
        {

            var score = new Score()
            {
                Username = reader.GetString(1),
                BeatmapHash = reader.GetString(3),
                TotalScore = reader.GetInt32(4),
                MaxCombo = reader.GetInt16(5),
                Mods = reader.GetString(7),
                Rank = reader.GetString(8),
                WhenPlayed = DateTime.ParseExact(reader.GetString(9), "yyMMddHHmmss", null),
                PlayMode = (PlayModes)reader.GetInt32(10),
            };

            scores.Add(score);
        }

        return scores;

    }


}