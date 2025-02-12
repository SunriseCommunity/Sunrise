using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Tests.Utils;

public class CharactersFilterTests
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
    public void IsValidStringCharacters_WithValidString_ReturnsTrue()
    {
        // Arrange
        var str = "test123";

        // Act
        var result = CharactersFilter.IsValidStringCharacters(str);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidCharacters))]
    public void IsValidStringCharacters_WithInvalidString_ReturnsFalse(string invalidCharacter)
    {
        var str = $"test123{invalidCharacter}";

        // Act
        var result = CharactersFilter.IsValidStringCharacters(str);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidUsernameCharacters_WithValidString_ReturnsTrue()
    {
        // Arrange
        var str = "test123";

        // Act
        var result = CharactersFilter.IsValidUsernameCharacters(str);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidUsernameCharacters))]
    public void IsValidUsernameCharacters_WithInvalidString_ReturnsFalse(string invalidCharacter)
    {
        var str = $"test123{invalidCharacter}";

        // Act
        var result = CharactersFilter.IsValidUsernameCharacters(str);

        // Assert

        if (invalidCharacter == " ")
            Assert.True(result);

        Assert.False(result);
    }

    [Fact]
    public void IsValidEmailCharacters_WithValidEmail_ReturnsTrue()
    {
        // Arrange
        var email = _mocker.User.GetRandomEmail();

        // Act
        var result = email.IsValidEmailCharacters();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidEmailCharacters_WithInvalidEmail_ReturnsFalse()
    {
        // Arrange
        var email = "test@";

        // Act
        var result = email.IsValidEmailCharacters();

        // Assert
        Assert.False(result);
    }
}