using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Utils.Tools;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

public class ApiUserAvatarUploadTests : ApiTest
{
    private const int Megabyte = 1024 * 1024;
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAvatarUploadWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        // Act
        var response = await client.PostAsync("user/upload/avatar", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAvatarUploadWithActiveRestriction()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        await Database.Users.Moderation.RestrictPlayer(user.Id, null, "Test");

        // Act
        var response = await client.PostAsync("user/upload/avatar", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAvatarUploadWithoutImage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        content.Add(new StringContent("value"), "fieldName");

        // Act
        var response = await client.PostAsync("user/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.NoFilesWereUploaded, responseError?.Detail);
    }

    [Fact]
    public async Task TestAvatarUploadWithInvalidFile()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        // Act
        var file = _mocker.GetRandomString();
        var response = await client.PostAsync("user/upload/avatar", new StringContent(file));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InvalidContentType, responseError?.Detail);
    }

    [Fact]
    public async Task TestAvatarUploadWithTooLargeImage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var imagePath = _fileService.GetRandomFilePath("png",
            new FileSizeFilter
            {
                MinSize = Megabyte * 5
            });

        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        content.Add(new ByteArrayContent(imageBytes), "file", "image.png");

        // Act
        var response = await client.PostAsync("user/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains("size", responseError?.Detail);
    }

    [Fact]
    public async Task TestAvatarUpload()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);


        var imagePath = _fileService.GetRandomFilePath("png",
            new FileSizeFilter
            {
                MaxSize = Megabyte * 5
            });

        await using var imageBytes = File.OpenRead(imagePath);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        content.Add(new StreamContent(imageBytes), "file", "image.png");

        // Act
        var response = await client.PostAsync("user/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newAvatar = await Database.Users.Files.GetAvatar(user.Id);

        var resizedUploadedImage = ImageTools.ResizeImage(imageBytes, 256, 256);

        Assert.NotNull(newAvatar);

        Assert.Equal(newAvatar, resizedUploadedImage);
    }

    [Fact]
    public async Task TestAvatarUploadWithNotImageFile()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var user = await CreateTestUser();
        var tokens = await GetUserAuthTokens(user);
        client.UseUserAuthToken(tokens);

        var textFilePath = _fileService.GetRandomFilePath("txt");
        var imageBytes = await File.ReadAllBytesAsync(textFilePath);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        content.Add(new ByteArrayContent(imageBytes), "file", "image.png");

        // Act
        var response = await client.PostAsync("user/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains("image format", responseError?.Detail);
    }
}