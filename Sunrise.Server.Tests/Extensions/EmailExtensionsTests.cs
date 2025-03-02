using Sunrise.Shared.Extensions.Users;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Server.Tests.Extensions;

public class EmailExtensionsTests
{
    private readonly MockService _mocker = new();

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