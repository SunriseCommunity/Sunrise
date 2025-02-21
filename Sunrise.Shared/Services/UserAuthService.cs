using System.Net;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Users;

namespace Sunrise.Shared.Services;

public class UserAuthService(RegionService regionService)
{
    public async Task<(User?, Dictionary<string, List<string>>?)> RegisterUser(string username, string password, string email, IPAddress ip)
    {
        var errors = new Dictionary<string, List<string>>
        {
            ["username"] = [],
            ["user_email"] = [],
            ["password"] = []
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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var foundUserByEmail = await database.UserService.GetUser(email: email);

        if (foundUserByEmail != null) errors["user_email"].Add("User with this email already exists.");

        var foundUserByUsername = await database.UserService.GetUser(username: username);

        if (foundUserByUsername != null && foundUserByUsername.IsActive()) errors["username"].Add("User with this username already exists.");

        var isUserCreatedAccountBefore = await database.EventService.UserEvent.IsIpCreatedAccountBefore(ip.ToString());
        if (isUserCreatedAccountBefore && !Configuration.IsDevelopment)
            errors["username"].Add("Please don't create multiple accounts. You have been warned.");

        if (errors.Any(x => x.Value.Count > 0))
            return (null, errors);

        var passhash = password.GetPassHash();
        var location = await regionService.GetRegion(ip);

        if (foundUserByUsername != null)
        {
            await database.UserService.UpdateUserUsername(
                foundUserByUsername,
                foundUserByUsername.Username,
                foundUserByUsername.Username.SetUsernameAsOld());
        }

        var newUser = new User
        {
            Username = username,
            Email = email,
            Passhash = passhash,
            Country = RegionService.GetCountryCode(location.Country),
            Privilege = UserPrivilege.User
        };

        newUser = await database.UserService.InsertUser(newUser);

        await database.EventService.UserEvent.CreateNewUserRegisterEvent(newUser.Id, ip.ToString(), newUser);

        return (newUser, null);
    }
}