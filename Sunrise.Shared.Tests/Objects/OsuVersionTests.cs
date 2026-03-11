namespace Sunrise.Shared.Tests.Objects;

using Sunrise.Shared.Objects;

public class OsuVersionParsingTests
{
    [Theory]
    [InlineData("b20260101.1", "stable40", 2026, 1, 1, 1)]
    [InlineData("b20260101", "stable40", 2026, 1, 1, 0)]
    [InlineData("b20231225.10", "stable40", 2023, 12, 25, 10)]
    public void TryParse_StableVersions_ParsesCorrectly(string input, string expectedStream, int year, int month, int day, int revision)
    {
        var version = OsuVersion.TryParse(input);

        Assert.NotNull(version);
        Assert.Equal(expectedStream, version.Stream);
        Assert.Equal(new DateTime(year, month, day), version.Date);
        Assert.Equal(revision, version.Revision);
    }

    [Fact]
    public void TryParse_CuttingEdgeVersion_ParsesCorrectly()
    {
        var version = OsuVersion.TryParse("b20240826.2cuttingedge");

        Assert.NotNull(version);
        Assert.Equal("cuttingedge", version.Stream);
        Assert.Equal(new DateTime(2024, 8, 26), version.Date);
        Assert.Equal(2, version.Revision);
    }

    [Fact]
    public void TryParse_BetaVersion_TreatedAsStable()
    {
        var version = OsuVersion.TryParse("b20241029.1beta");

        Assert.NotNull(version);
        Assert.Equal("stable40", version.Stream);
        Assert.Equal(new DateTime(2024, 10, 29), version.Date);
        Assert.Equal(1, version.Revision);
    }

    [Fact]
    public void TryParse_StableWithoutRevision_DefaultsToZero()
    {
        var version = OsuVersion.TryParse("b20260101");

        Assert.NotNull(version);
        Assert.Equal("stable40", version.Stream);
        Assert.Equal(0, version.Revision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    public void TryParse_InvalidInputs_ReturnsNull(string? input)
    {
        var version = OsuVersion.TryParse(input!);
        Assert.Null(version);
    }

    [Fact]
    public void Parse_WithExplicitStream_ParsesCorrectly()
    {
        var version = OsuVersion.Parse("stable40", "20260101.1");

        Assert.NotNull(version);
        Assert.Equal("stable40", version.Stream);
        Assert.Equal(new DateTime(2026, 1, 1), version.Date);
        Assert.Equal(1, version.Revision);
    }

    [Fact]
    public void Parse_ChangelogVersionFormat_ParsesCorrectly()
    {
        var version = OsuVersion.Parse("cuttingedge", "20240826.2");

        Assert.NotNull(version);
        Assert.Equal("cuttingedge", version.Stream);
        Assert.Equal(new DateTime(2024, 8, 26), version.Date);
        Assert.Equal(2, version.Revision);
    }
}

public class OsuVersionComparisonTests
{
    [Fact]
    public void NewerDate_IsGreater()
    {
        var older = OsuVersion.TryParse("b20250101.1");
        var newer = OsuVersion.TryParse("b20260101.1");

        Assert.True(newer > older);
        Assert.True(older < newer);
        Assert.False(older > newer);
        Assert.False(newer < older);
    }

    [Fact]
    public void SameDate_HigherRevision_IsGreater()
    {
        var lower = OsuVersion.TryParse("b20260101.1");
        var higher = OsuVersion.TryParse("b20260101.5");

        Assert.True(higher > lower);
        Assert.True(lower < higher);
    }

    [Fact]
    public void EqualVersions_AreEqual()
    {
        var v1 = OsuVersion.TryParse("b20260101.1");
        var v2 = OsuVersion.TryParse("b20260101.1");

        Assert.True(v1 == v2);
        Assert.False(v1 != v2);
        Assert.False(v1 > v2);
        Assert.False(v1 < v2);
    }

    [Fact]
    public void CrossStream_ComparisonByDate()
    {
        var stable = OsuVersion.TryParse("b20260101.1");
        var cuttingEdge = OsuVersion.TryParse("b20240826.2cuttingedge");

        Assert.True(stable > cuttingEdge);
        Assert.True(cuttingEdge < stable);
    }

    [Fact]
    public void NullComparisons_HandleCorrectly()
    {
        var version = OsuVersion.TryParse("b20260101.1");

        Assert.True(version > null);
        Assert.False(version < null);
        Assert.True(null < version);
        Assert.False(null > version);
        Assert.True((OsuVersion?)null == null);
        Assert.False(version == null);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var version = OsuVersion.TryParse("b20260101.1");

        Assert.NotNull(version);
        Assert.Equal("20260101.1", version.ToString());
    }
}
