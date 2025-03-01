using Sunrise.Shared.Extensions;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Server.Tests.Extensions;

public class UsernameExtensionsTests
{

    private static readonly string[] InvalidCharacters = [" ", "\ud83d\ude02", "ä", "漢", "/", "ё"];
    private readonly MockService _mocker = new();

    public static IEnumerable<object[]> GetInvalidCharacters()
    {
        return InvalidCharacters.Select(c => new object[]
        {
            c
        });
    }

    public static IEnumerable<object[]> GetInvalidUsernameCharacters()
    {
        return GetInvalidCharacters().Where(c => (string)c[0] != " ");
    }

    [Fact]
    public void IsValidUsernameCharacters_WithValidString_ReturnsTrue()
    {
        // Arrange
        var str = "test123";

        // Act
        var result = str.IsValidStringCharacters();

        // Assert
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidUsernameCharacters))]
    public void IsValidUsernameCharacters_WithInvalidString_ReturnsFalse(string invalidCharacter)
    {
        var str = $"test123{invalidCharacter}";

        // Act
        var result = str.IsValidStringCharacters();

        // Assert

        if (invalidCharacter == " ")
            Assert.True(result);

        Assert.False(result);
    }
}