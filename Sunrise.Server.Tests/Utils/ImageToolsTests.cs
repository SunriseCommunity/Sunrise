using SixLabors.ImageSharp;
using Sunrise.Server.Tests.Core;
using Sunrise.Server.Tests.Core.Abstracts;
using Sunrise.Server.Tests.Core.Utils;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Tests.Utils;

public class ImageToolsTests : FilesystemTest
{
    [Fact]
    public void TestResizeImage()
    {
        // Arrange
        var imagePath = FileMockUtil.GetRandomImageFilePath("jpg");
        
        var imageBytes = File.ReadAllBytes(imagePath);
        var size = new Size(100, 100);
        
        // Act
        var resizedImage = ImageTools.ResizeImage(imageBytes, size.Height, size.Width);
        
        // Assert
        using var image = Image.Load(resizedImage);
        Assert.Equal(size, image.Size);
    }
}