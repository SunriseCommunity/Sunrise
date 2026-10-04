using Sunrise.Server.Commands.ChatCommands.Moderation;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Enums.Users;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Services.Mock;
using Sunrise.Tests;

namespace Sunrise.Server.Tests.Repositories;

[Collection("Integration tests collection")]
public class BatPrivilegeRegressionTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private readonly MockService _mocker = new();

    [Fact]
    public async Task SingleModeBatCanSeeBatCommandsInAvailableCommands()
    {
        var user = await CreateTestUser();
        user.Privilege = UserPrivilege.BeatmapApprovalTeamMania;
        await Database.Users.UpdateUser(user);
        var session = CreateTestSession(user);

        ChatCommandRepository.GetHandlers();
        var commands = ChatCommandRepository.GetAvailableCommands(session);

        Assert.Contains("setbeatmapstatus", commands);
        Assert.Contains("setbeatmapsetstatus", commands);
    }

    [Fact]
    public async Task AdminCanChangeCountryOfBatUser()
    {
        var (session, executor) = await CreateTestSession();
        executor.Privilege = UserPrivilege.Admin;
        await Database.Users.UpdateUser(executor);
        var target = _mocker.User.GetRandomUser();
        target.Privilege = UserPrivilege.BeatmapApprovalTeamStandard;
        target.Country = Sunrise.Shared.Enums.Users.CountryCode.US;
        await CreateTestUser(target);

        await new CountryCommand().Handle(session, null, [target.Id.ToString(), "GB"]);

        var updatedTarget = await Database.Users.GetUser(target.Id);
        Assert.NotNull(updatedTarget);
        Assert.Equal(CountryCode.GB, updatedTarget.Country);
    }

    [Fact]
    public async Task AdminCanChangeUsernameOfBatUser()
    {
        var (session, executor) = await CreateTestSession();
        executor.Privilege = UserPrivilege.Admin;
        await Database.Users.UpdateUser(executor);
        var target = _mocker.User.GetRandomUser();
        target.Privilege = UserPrivilege.BeatmapApprovalTeamStandard;
        await CreateTestUser(target);
        var newUsername = $"changed_{_mocker.GetRandomString(8)}";

        await new UsernameCommand().Handle(session, null, [target.Id.ToString(), newUsername]);

        var updatedTarget = await Database.Users.GetUser(target.Id);
        Assert.NotNull(updatedTarget);
        Assert.Equal(newUsername, updatedTarget.Username);
    }
}
