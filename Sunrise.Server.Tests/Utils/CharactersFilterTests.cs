using Sunrise.Server.Tests.Core.Services.Mock;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Tests.Utils;

public class CharactersFilterTests
{
    private readonly MockService _mocker = new();
    
    private static readonly string[] InvalidCharacters = [" ", "\ud83d\ude02", "ä", "漢", "/"];

    public static IEnumerable<object[]> GetInvalidCharacters()
    {
        return InvalidCharacters.Select(c => new object[] { c });
    }

    [Fact] 
    public void IsValidString_WithValidString_ReturnsTrue()
    {
        // Arrange
        var str = "test123";
        
        // Act
        var result = CharactersFilter.IsValidString(str);
        
        // Assert
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidCharacters))]
    public void IsValidString_WithInvalidString_ReturnsFalse(string invalidCharacter)
    {
        var str = $"test123{invalidCharacter}";
        
        // Act
        var result = CharactersFilter.IsValidString(str);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void IsValidString_WithValidRussianString_ReturnsTrue()
    {
        // Arrange
        var str = "тест123";
        
        // Act
        var result = CharactersFilter.IsValidString(str, true);
        
        // Assert
        Assert.True(result);
    }
    
    [Theory]
    [MemberData(nameof(GetInvalidCharacters))]
    public void IsValidString_WithInvalidRussianString_ReturnsFalse(string invalidCharacter)
    {
        var str = $"тест123{invalidCharacter}";
        
        // Act
        var result = CharactersFilter.IsValidString(str, true);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithValidEmail_ReturnsTrue()
    {
        // Arrange
        var email = _mocker.User.GetRandomEmail();

        // Act
        var result = email.IsValidEmail();

        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsValidEmail_WithInvalidEmail_ReturnsFalse()
    {
        // Arrange
        var email = "test@";

        // Act
        var result = email.IsValidEmail();

        // Assert
        Assert.False(result);
    }

}