using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Scores;
using Xunit;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Tests.Database.Models;

public class UserGradesTests
{
    [Fact]
    public void UpdateGradeCountUpdatesAndClampsTheGradeCount()
    {
        var grades = new UserGrades { UserId = 1, GameMode = GameMode.Standard };

        grades.UpdateGradeCount(ScoreGrade.A, 1);
        grades.UpdateGradeCount(ScoreGrade.A, -1);
        grades.UpdateGradeCount(ScoreGrade.A, -1);

        Assert.Equal(0, grades.CountA);
    }
}
