using System.ComponentModel;
using System.Globalization;
using Sunrise.Enums;
using static System.Int32;

namespace Sunrise.Types.Objects;

public class ScoreObject
{
    public Score Score { get; private set; }

    public ScoreObject(string scoreString)
    {
        Score = ParseStringToScore(scoreString);
    }


    private static Score ParseStringToScore(string scoreString)
    {
        var dividedString = scoreString.Split(":");

        var score = new Score()
        {
            BeatmapHash = dividedString[0],
            Username = dividedString[1],
            OnlineChecksum = dividedString[2],
            Count300 = ushort.Parse(dividedString[3]),
            Count100 = ushort.Parse(dividedString[4]),
            Count50 = ushort.Parse(dividedString[5]),
            CountGeki = ushort.Parse(dividedString[6]),
            CountKatu = ushort.Parse(dividedString[7]),
            CountMiss = ushort.Parse(dividedString[8]),
            TotalScore = Parse(dividedString[9]),
            MaxCombo = ushort.Parse(dividedString[10]),
            IsFullCombo = bool.Parse(dividedString[11]),
            Rank = dividedString[12],
            Mods = dividedString[13],
            IsMapPassed = bool.Parse(dividedString[14]),
            PlayMode = (PlayModes)Parse(dividedString[15]),
            WhenPlayed = DateTime.TryParseExact(dividedString[16], "yyMMddHHmmss", null, DateTimeStyles.None, out var date) ? date : DateTime.Now,
            OsuVersion = dividedString[17],
        };

        return score;
    }

    [Description("Uploads the score to the database.")]
    public void UploadScore()
    {
        var scoreCheckSum = GetScoreCheckSum();

        // TODO: Implement score uploading
    }

    private string GetScoreCheckSum()
    {
        var scoreData =
            $"SunriseScore{Score.BeatmapHash}{Score.Username}{Score.TotalScore}{Score.Mods}{Score.WhenPlayed}{Score.OsuVersion}";

        var scoreCheckSum = BitConverter
            .ToString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scoreData))).Replace("-", string.Empty);

        return scoreCheckSum;
    }

}

