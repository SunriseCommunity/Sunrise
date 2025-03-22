using Sunrise.Shared.Extensions;

namespace Sunrise.Server.Tests.Extensions;

public class StringExtensionsTests
{
    private static readonly string[] InvalidCharacters = [" ", "\ud83d\ude02", "ä", "漢", "/", "ё"];

    public static IEnumerable<object[]> GetInvalidCharacters()
    {
        return InvalidCharacters.Select(c => new object[]
        {
            c
        });
    }

    [Fact]
    public void IsValidStringCharacters_WithValidString_ReturnsTrue()
    {
        // Arrange
        var str = "test123";

        // Act
        var result = str.IsValidStringCharacters();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidCharacters))]
    public void IsValidStringCharacters_WithInvalidString_ReturnsFalse(string invalidCharacter)
    {
        // Arrange
        var str = $"test123{invalidCharacter}";

        // Act
        var result = str.IsValidStringCharacters();

        // Assert
        Assert.False(result);
    }
}