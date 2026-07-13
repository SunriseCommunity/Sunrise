using Sunrise.Processing.Utils;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects.Serializable;
using Xunit;

namespace Sunrise.Processing.Tests.Utils;

public class ReplayValidationUtilTests
{
    [Fact]
    public void RejectsTruncatedReplay()
    {
        var result = ReplayValidationUtil.Validate([0x5d, 0, 0], new Beatmap(), new Score());
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidReplay, result.Error.Code);
    }

    [Fact]
    public void RejectsDeclaredDecompressionBomb()
    {
        var replay = new byte[13];
        replay[0] = 0x5d;
        BitConverter.GetBytes(32L * 1024 * 1024).CopyTo(replay, 5);

        var result = ReplayValidationUtil.Validate(replay, new Beatmap(), new Score());
        Assert.True(result.IsFailure);
        Assert.Equal(ScoreProcessingErrorCode.InvalidReplay, result.Error.Code);
    }
}
