﻿using Sunrise.Database;
using Sunrise.Database.Schemas;
using Sunrise.Utils;

namespace Sunrise.GameClient.Services;


[Obsolete("This class is going to be refactored. Please do not edit it.")]
public class ScoreService
{
    private readonly ServicesProvider _services;

    public ScoreService(ServicesProvider services)
    {
        _services = services;
    }

    public async Task<string> SubmitScore(HttpRequest request)
    {
        var scoreEncoded = request.Form["score"];
        var osuver = request.Form["osuver"];
        var iv = request.Form["iv"];
        var pass = request.Form["pass"];

        if (string.IsNullOrEmpty(scoreEncoded) || string.IsNullOrEmpty(osuver) || string.IsNullOrEmpty(iv) || string.IsNullOrEmpty(pass))
        {
            throw new Exception("Invalid request");
        }

        // Get score from encoded string
        var decodedString = ScoreDecoder.Decode(scoreEncoded!, iv!, osuver!);
        var scoreschema = new Score();
        var schema = await scoreschema.SetScoreFromString(decodedString, _services);
        var scores = await _services.Database.GetScores(schema.BeatmapHash);

        Score? prevPersonalBest = null;

        if (scores.Count > 0)
        {
            var personalBestList = scores.FindAll(x => x.UserId == schema.UserId);
            if (personalBestList.Count > 0)
            {
                prevPersonalBest = personalBestList.OrderByDescending(x => x.TotalScore).First();
            }
        }

        var user = await _services.Database.GetUser(id: schema.UserId);
        var oldUser = await _services.Database.GetUser(id: schema.UserId); // Note: This is bad. But I'm tired to implement something better.

        // Update user stats if new score is better
        if (prevPersonalBest != null && schema.TotalScore > prevPersonalBest.TotalScore)
        {
            user.RankedScore += schema.TotalScore - prevPersonalBest.TotalScore;

            // TODO: Implement proper calculation for accuracy
            // user.Accuracy = (user.Accuracy * (user.PlayCount - 1) + schema.Accuracy) / user.PlayCount;
        }
        else if (prevPersonalBest == null)
        {
            user.RankedScore += schema.TotalScore; // First score
        }

        user.PlayCount += 1;
        user.TotalScore += schema.TotalScore;
        await _services.Database.UpdateUser(user);

        var mapRankBefore = scores.FindIndex(x => x.Id == prevPersonalBest?.Id);

        // Add score to old scores and get new map rank
        var score = await _services.Database.InsertScore(schema);
        if (prevPersonalBest == null)
        {
            // Remove all scores for current user
            scores = scores.FindAll(x => x.UserId != schema.UserId);
        }

        scores.Add(score);
        scores = scores.GroupBy(x => x.UserId).Select(x => x.OrderByDescending(y => y.TotalScore).First()).ToList();

        var mapRankAfter = scores.FindIndex(x => x.Id == score.Id);

        var response = GetScoreSubmitResponse(schema, user, oldUser, prevPersonalBest, mapRankBefore, mapRankAfter);
        return response;
    }

    private string GetScoreSubmitResponse(Score score, User user, User prevUser, Score? prevScore, int mapRankBefore, int mapRankAfter)
    {
        // TODO: This is a mock response. Implement proper response
        const int beatmapId = 1;
        const int beatmapSetId = 1;
        const int beatmapPlaycount = 1;
        const int beatmapPasscount = 1;
        const string approvedDate = "2018-11-12 13:48:26";
        const int beatmapSetUrl = 1;
        const int userUrl = 1;
        var prevMapRank = mapRankBefore > 0 ? (mapRankBefore + 1).ToString() : "";

        var beatmapInfo = $"beatmapId:{beatmapId}|beatmapSetId:{beatmapSetId}|beatmapPlaycount:{beatmapPlaycount}|beatmapPasscount:{beatmapPasscount}|approvedDate:{approvedDate}";
        var beatmapRanking = $"chartId:beatmap|chartUrl:{beatmapSetUrl}|chartName:Beatmap Ranking";
        var scoreInfo = string.Join("|",
            new List<string>
            {

                GetChartEntry("rank",  prevMapRank , mapRankAfter+1), GetChartEntry("rankedScore", prevScore?.TotalScore, score.TotalScore), GetChartEntry("totalScore", prevScore?.TotalScore, score.TotalScore), GetChartEntry("maxCombo", prevScore?.MaxCombo, score.MaxCombo), GetChartEntry("accuracy", prevScore?.Accuracy, score.Accuracy), GetChartEntry("pp", 0, 0), // TODO: Change to real PP values
            });
        // TODO: Change user.Id to user.Rank
        var playerInfo = $"chartId:overall|chartUrl:{userUrl}|chartName:Overall Ranking|" + string.Join("|",
            new List<string>
            {
                GetChartEntry("rank", prevUser.Id, user.Id), GetChartEntry("rankedScore", prevUser.RankedScore, user.RankedScore), GetChartEntry("totalScore", prevUser.TotalScore, user.TotalScore), GetChartEntry("maxCombo", prevUser.PlayCount, user.PlayCount), GetChartEntry("accuracy", prevUser.Accuracy, user.Accuracy), GetChartEntry("pp", prevUser.PerformancePoints, user.PerformancePoints),
            });

        return $"{beatmapInfo}\n{beatmapRanking}|{scoreInfo}|onlineScoreId:{score.Id}\n{playerInfo}|achievements-new:";
    }


    private static string GetChartEntry(string name, object? before, object? after)
    {
        return $"{name}Before:{before?.ToString() ?? string.Empty}|{name}After:{after?.ToString() ?? string.Empty}";
    }


    public async Task<string> GetBeatmapScores(HttpRequest Request)
    {
        var hash = Request.Query["c"];
        var mode = Request.Query["m"];
        var username = Request.Query["us"];

        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(username))
        {
            throw new Exception("Invalid request");
        }

        var scores = await _services.Database.GetScores(hash);

        // TODO: Change to proper implementation
        var ranked_status = "2";
        var beatmap_id = "1228322";
        var beatmap_set_id = "535277";
        var beatmap_name = "Ariabl'eyeS - Kegare Naki Bara Juuji (Short ver.) [Extra]";

        // Get personal best for each
        scores = scores.GroupBy(x => x.UserId).Select(x => x.OrderByDescending(y => y.TotalScore).First()).ToList();
        scores.Sort((x, y) =>
            y.TotalScore.CompareTo(x.TotalScore) != 0
                ? y.TotalScore.CompareTo(x.TotalScore)
                : x.WhenPlayed.CompareTo(y.WhenPlayed));

        var responses = new string[]
        {
                $"{ranked_status}|false|{beatmap_id}|{beatmap_set_id}|{scores.Count}",
                $"0\n{beatmap_name}\n0.0"
        };

        if (scores.Count == 0)
        {
            return string.Join("\n", responses);
        }

        scores = scores.Take(50).ToList();

        var user = await _services.Database.GetUser(username: username);

        var personalBest = scores.FindIndex(x => x.UserId == user?.Id);
        if (personalBest != -1)
        {
            var score = scores[personalBest];
            responses = responses.Append(await score.GetString(personalBest + 1, _services)).ToArray();
        }
        else
        {
            responses = responses.Append("").ToArray();
        }

        for (int i = 0; i < scores.Count && i < 50; i++)
        {
            var score = scores[i];
            var response = await score.GetString(i + 1, _services);
            responses = responses.Append(response).ToArray();
        }

        return string.Join("\n", responses);
    }
}