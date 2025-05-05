using SixLabors.ImageSharp;
using Sunrise.Shared.Utils.Tools;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Server.Tests.Utils;

public class ImageToolsTests : FilesystemTest
{

    private const int Megabyte = 1024 * 1024;
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();

    [Fact]
    public void TestResizeImage()
    {
        // Arrange
        var imagePath = _fileService.GetRandomFilePath("jpg");

        using var imageBytes = File.OpenRead(imagePath);
        var size = new Size(100, 100);

        // Act
        var resizedImage = ImageTools.ResizeImage(imageBytes, size.Height, size.Width);

        // Assert
        using var image = Image.Load(resizedImage);
        Assert.Equal(size, image.Size);
    }

    [Fact]
    public void TestResizeImageWithInvalidSize()
    {
        // Arrange
        var imagePath = _fileService.GetRandomFilePath("jpg");

        using var imageBytes = File.OpenRead(imagePath);
        var size = new Size(0, -100);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ImageTools.ResizeImage(imageBytes, size.Height, size.Width));
    }

    [Fact]
    public void TestResizeImageWithInvalidImage()
    {
        // Arrange
        using var imageBytes = new MemoryStream();
        var size = new Size(100, 100);

        // Act & Assert
        Assert.Throws<UnknownImageFormatException>(() => ImageTools.ResizeImage(imageBytes, size.Height, size.Width));
    }

    [Fact]
    public void TestResizeImageWithNotImage()
    {
        // Arrange
        var textFilePath = _fileService.GetRandomFilePath("txt");
        
        using var imageBytes = File.OpenRead(textFilePath);
        var size = new Size(100, 100);

        // Act & Assert
        Assert.Throws<UnknownImageFormatException>(() => ImageTools.ResizeImage(imageBytes, size.Height, size.Width));
    }

    [Fact]
    public void TestIsValidImageCheckValidImage()
    {
        // Arrange
        var imagePath = _fileService.GetRandomFilePath("jpg");
        var imageBytes = File.ReadAllBytes(imagePath);

        // Act
        var result = ImageTools.IsValidImage(new MemoryStream(imageBytes));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestIsValidImageCheckInvalidImage()
    {
        // Arrange
        var textFilePath = _fileService.GetRandomFilePath("txt");
        var imageBytes = File.ReadAllBytes(textFilePath);

        // Act
        var result = ImageTools.IsValidImage(new MemoryStream(imageBytes));

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("ico")]
    public void TestGetImageType(string extension)
    {
        // Arrange
        var imagePath = _fileService.GetRandomFilePath(extension);
        var imageBytes = File.ReadAllBytes(imagePath);

        // Act
        var result = ImageTools.GetImageType(imageBytes);

        // Assert
        Assert.Equal(extension, result);
    }

    [Fact]
    public void IsHasValidImageAttributesWithValidImage()
    {
        // Arrange
        var imagePath = _fileService.GetRandomFilePath("jpg",
            new FileSizeFilter
            {
                MaxSize = Megabyte * 5
            });
        var imageBytes = File.ReadAllBytes(imagePath);

        // Act
        var result = ImageTools.IsHasValidImageAttributes(new MemoryStream(imageBytes));

        // Assert
        Assert.True(result.Item1);
    }

    [Fact]
    public void IsHasValidImageAttributesWithLargeImage()
    {
        // Arrange
        var imagePath = _fileService.GetRandomFilePath("png",
            new FileSizeFilter
            {
                MinSize = Megabyte * 5
            });
        var imageBytes = File.ReadAllBytes(imagePath);

        // Act
        var result = ImageTools.IsHasValidImageAttributes(new MemoryStream(imageBytes));

        // Assert
        Assert.False(result.Item1);
    }

    [Fact]
    public void IsHasValidImageAttributesWithInvalidImage()
    {
        // Arrange
        var textFilePath = _fileService.GetRandomFilePath("txt");
        var imageBytes = File.ReadAllBytes(textFilePath);

        // Act
        var result = ImageTools.IsHasValidImageAttributes(new MemoryStream(imageBytes));

        // Assert
        Assert.False(result.Item1);
    }

    [Fact]
    public void TestGetImageTypeWithInvalidImage()
    {
        // Arrange
        var textFilePath = _fileService.GetRandomFilePath("txt");
        var imageBytes = File.ReadAllBytes(textFilePath);

        // Act
        var result = ImageTools.GetImageType(imageBytes);

        // Assert
        Assert.Null(result);
    }
}