using System.Net;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Users;

namespace Sunrise.Shared.Services;

public class UserAuthService(RegionService regionService, DatabaseService database)
{
    public async Task<(User?, Dictionary<string, List<string>>?)> RegisterUser(string username, string password, string email, IPAddress ip)
    {
        var errors = new Dictionary<string, List<string>>
        {
            ["user_email"] = [],
            ["password"] = [],
            ["username"] = []
        };

        if (Configuration.BannedIps.Contains(ip.ToString()))
            errors["username"].Add("Your IP address is banned. Please contact support.");

        var (isUsernameValid, usernameError) = username.IsValidUsername();
        if (!isUsernameValid)
            errors["username"].Add(usernameError ?? "Invalid username");

        if (!email.IsValidStringCharacters() || !email.IsValidEmailCharacters())
            errors["user_email"].Add("Invalid email. It should be a valid email address.");

        var (isPasswordValid, passwordError) = password.IsValidPassword();
        if (!isPasswordValid)
            errors["password"].Add(passwordError ?? "Invalid password");

        var foundUserByEmail = await database.Users.GetUser(email: email);

        if (foundUserByEmail != null) errors["user_email"].Add("User with this email already exists.");

        var foundUserByUsername = await database.Users.GetUser(username: username);
        if (foundUserByUsername != null && foundUserByUsername.IsActive()) errors["username"].Add("User with this username already exists.");

        var accountCreatedFromSameIp = await database.Events.Users.IsIpHasAnyRegisteredAccounts(ip.ToString());
        if (accountCreatedFromSameIp != null && !Configuration.IsDevelopment)
            errors["username"].Add($"Please don't create multiple accounts. You have been warned.\nYou previously created account with name: \"{accountCreatedFromSameIp.Username}\".\nContact support if you can't for some reason use it.");

        if (errors.Any(x => x.Value.Count > 0))
            return (null, errors);

        var passhash = password.GetPassHash();
        var location = await regionService.GetRegion(ip);

        if (foundUserByUsername != null && foundUserByUsername.IsActive() == false)
        {
            var updateUsernameResult = await database.Users.UpdateUserUsername(
                foundUserByUsername,
                foundUserByUsername.Username,
                foundUserByUsername.Username.SetUsernameAsOld());

            if (updateUsernameResult.IsFailure)
            {
                errors["username"].Add(updateUsernameResult.Error);
                return (null, errors);
            }
        }

        var newUser = new User
        {
            Username = username,
            Email = email,
            Passhash = passhash,
            Country = RegionService.GetCountryCode(location.Country),
            Privilege = UserPrivilege.User
        };

        var addUserResult = await database.Users.AddUser(newUser);

        if (addUserResult.IsFailure)
        {
            errors["username"].Add(addUserResult.Error);
            return (null, errors);
        }

        await database.Events.Users.AddUserRegisterEvent(newUser.Id, ip.ToString(), newUser);

        return (newUser, null);
    }
}