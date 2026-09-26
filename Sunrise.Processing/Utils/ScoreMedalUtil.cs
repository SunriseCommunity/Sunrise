using Sunrise.Shared.Database.Models;

namespace Sunrise.Processing.Utils;

public static class ScoreMedalUtil
{
    public static string GetMedalScoreSubmissionResultString(Medal medal)
    {
        return $"{medal.Id}+{medal.Name}+{medal.Description}";
    }
}