using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Database.Sqlite;
using Sunrise.Types.Objects;
using Sunrise.Utils;

namespace Sunrise.WebServer.Controllers;

[Controller]
[Route("/web")]
public class ScoreController : ControllerBase
{
    private readonly SqliteDatabase _database;

    public ScoreController(SqliteDatabase database)
    {
        _database = database;
    }

    [HttpPost]
    [Route("osu-submit-modular-selector.php")]
    [Description("Handles score submission.")]
    public Task<IActionResult> Submit()
    {
        var scoreEncoded = Request.Form["score"];
        var osuver = Request.Form["osuver"];
        var iv = Request.Form["iv"];
        var pass = Request.Form["pass"];

        if (string.IsNullOrEmpty(scoreEncoded) || string.IsNullOrEmpty(osuver) || string.IsNullOrEmpty(iv) || string.IsNullOrEmpty(pass))
        {
            return Task.FromResult<IActionResult>(BadRequest("error: beatmap"));
        }

        var decodedString = ScoreDecoder.Decode(scoreEncoded, iv, osuver, null);
        var score = new ScoreObject(decodedString);

        // Handle score submission
        _database.Scores.AddScore(score);

        // Mock response
        // TODO: Implement proper response
        string respone = String.Empty;
        respone += "beatmapId:" + 1 + "|";
        respone += "beatmapSetId:" + 1 + "|";
        respone += "beatmapPlaycount:" + 1 + "|";
        respone += "beatmapPasscount:" + 1 + "|";
        respone += "approvedDate:" + 1 + "\n";
        respone += "chartId:" + 1 + "|";
        respone += "chartName:" + 1 + "|";
        respone += "chartEndDate:" + 1 + "|";
        respone += "beatmapRankingBefore:" + 1 + "|";
        respone += "beatmapRankingAfter:" + 1 + "|";
        respone += "rankedScoreBefore:" + 1 + "|";
        respone += "rankedScoreAfter:" + 1 + "|";
        respone += "totalScoreBefore:" + 1 + "|";
        respone += "totalScoreAfter:" + 1 + "|";
        respone += "playCountBefore:" + 1 + "|";
        respone += "accuracyBefore:" + 1 + "|";
        respone += "accuracyAfter:" + 1 + "|";
        respone += "rankBefore:" + 1 + "|";
        respone += "rankAfter:" + 1 + "|";
        respone += "toNextRank:" + 1 + "|";
        respone += "toNextRankUser:" + 1 + "|";
        respone += "achievements:" + 1 + "\n";

        return Task.FromResult<IActionResult>(Ok(respone));
    }

    [HttpGet]
    [Route("osu-osz2-getscores.php")]
    [Description("Handles score fetching for beatmap.")]
    public IActionResult GetScores()
    {
        var hash = Request.Query["c"];
        var mode = Request.Query["m"];

        var scores = _database.Scores.GetScores(hash);

        // TODO: Change to proper implementation
        var ranked_status = "2";
        var beatmap_id = "1228322";
        var beatmap_set_id = "535277";
        var beatmap_name = "Ariabl'eyeS - Kegare Naki Bara Juuji (Short ver.) [Extra]";

        var responses = new string[]
        {
                $"{ranked_status}|false|{beatmap_id}|{beatmap_set_id}|{scores.Count}",
                $"0\n{beatmap_name}\n0.0\n"
        };

        if (scores.Count == 0)
        {
            return Ok(string.Join("\n", responses));
        }

        scores = scores.GroupBy(x => x.Username).Select(x => x.OrderByDescending(y => y.TotalScore).First()).OrderByDescending(x => x.TotalScore).ToList();
        scores.Sort((x, y) =>
            y.TotalScore.CompareTo(x.TotalScore) != 0
                ? y.TotalScore.CompareTo(x.TotalScore)
                : y.WhenPlayed.CompareTo(x.WhenPlayed));
        scores = scores.Take(50).ToList();

        for (int i = 0; i < scores.Count && i < 50; i++)
        {
            var score = scores[i];
            PlayerObject player = _database.Players.GetPlayer(null, score.Username);

            var id = score.d;
            var name = score.Username;
            var totalscore = score.TotalScore;
            var max_combo = score.MaxCombo;
            var n50 = score.Count50;
            var n100 = score.Count100;
            var n300 = score.Count300;
            var nmiss = score.CountMiss;
            var nkatu = score.CountKatu;
            var ngeki = score.CountGeki;
            var perfect = score.IsFullCombo;
            var mods = score.Mods;
            var userid = player.Player.Id;
            var rank = i + 1;
            var time = score.WhenPlayed.ToString("yyMMddHHmmss");
            var has_replay = "1";

            var response =
                $"{id}|{name}|{totalscore}|{max_combo}|{n50}|{n100}|{n300}|{nmiss}|{nkatu}|{ngeki}|{perfect}|{mods}|{userid}|{rank}|{time}|{has_replay}";

            responses = responses.Append(response).ToArray();
        }

        return Ok(string.Join("\n", responses));
    }


}