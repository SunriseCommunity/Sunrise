using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Objects.Keys;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Utils.Tools;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Extensions;
using Sunrise.Tests;
using Sunrise.Tests.Services;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.API.UserController;

[Collection("Integration tests collection")]
public class ApiAdminUploadUserAvatarTests(IntegrationDatabaseFixture fixture) : ApiTest(fixture)
{
    private const int Megabyte = 1024 * 1024;
    private readonly FileService _fileService = new();
    private readonly MockService _mocker = new();

    [Fact]
    public async Task TestAdminUploadUserAvatarWithoutAuthToken()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");
        var targetUser = await CreateTestUser();

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarWithNonAdminUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var regularUser = await CreateTestUser();
        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(regularUser);
        client.UseUserAuthToken(tokens);

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", new StringContent(""));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarWithInvalidId()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        var imagePath = _fileService.GetRandomFilePath("png", new FileSizeFilter { MaxSize = Megabyte * 5 });
        await using var imageBytes = File.OpenRead(imagePath);
        content.Add(new StreamContent(imageBytes), "file", "image.png");

        // Act
        var response = await client.PostAsync("user/999999/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.UserNotFound, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarWithoutImage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        content.Add(new StringContent("value"), "fieldName");

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.NoFilesWereUploaded, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarWithInvalidFile()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        // Act
        var file = _mocker.GetRandomString();
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", new StringContent(file));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains(ApiErrorResponse.Detail.InvalidContentType, responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarWithTooLargeImage()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
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
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains("size", responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatar()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
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
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newAvatar = await Database.Users.Files.GetAvatar(targetUser.Id);

        await using var imageBytesForComparison = File.OpenRead(imagePath);
        var resizedUploadedImage = ImageTools.ResizeImage(imageBytesForComparison, 256, 256);

        Assert.NotNull(newAvatar);

        Assert.Equal(newAvatar, resizedUploadedImage);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarWithNotImageFile()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();

        var tokens = await GetUserAuthTokens(adminUser);
        client.UseUserAuthToken(tokens);

        var textFilePath = _fileService.GetRandomFilePath("txt");
        var imageBytes = await File.ReadAllBytesAsync(textFilePath);

        using var content = new MultipartFormDataContent();
        content.Headers.ContentType!.MediaType = "multipart/form-data";
        content.Add(new ByteArrayContent(imageBytes), "file", "image.png");

        // Act
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseError = await response.Content.ReadFromJsonAsyncWithAppConfig<ProblemDetails>();
        Assert.Contains("image format", responseError?.Detail);
    }

    [Fact]
    public async Task TestAdminUploadUserAvatarForRestrictedUser()
    {
        // Arrange
        var client = App.CreateClient().UseClient("api");

        var adminUser = _mocker.User.GetRandomUser();
        adminUser.Privilege = UserPrivilege.Admin;
        await CreateTestUser(adminUser);

        var targetUser = await CreateTestUser();
        await Database.Users.Moderation.RestrictPlayer(targetUser.Id, null, "Test");

        var tokens = await GetUserAuthTokens(adminUser);
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
        var response = await client.PostAsync($"user/{targetUser.Id}/upload/avatar", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var newAvatar = await Database.Users.Files.GetAvatar(targetUser.Id);

        await using var imageBytesForComparison = File.OpenRead(imagePath);
        var resizedUploadedImage = ImageTools.ResizeImage(imageBytesForComparison, 256, 256);

        Assert.NotNull(newAvatar);

        Assert.Equal(newAvatar, resizedUploadedImage);
    }
}

