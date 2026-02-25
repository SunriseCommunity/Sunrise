using Sunrise.Server.Attributes;
using Sunrise.Server.Repositories;
using Sunrise.Shared.Application;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Commands.ChatCommands.SuperUser;

[ChatCommand("observatorydebug", requiredPrivileges: UserPrivilege.SuperUser)]
public class ObservatoryDebug : IChatCommand
{
    public async Task Handle(Session session, ChatChannel? channel, string[]? args)
    {
        using var httpClient = new HttpClient();

        var apis = Configuration.ExternalApis.Where(x => x.Type == ApiType.GetObservatoryStats).OrderBy(x => x.Priority).ToList();

        var observatoryStatsApi = apis.FirstOrDefault();

        if (observatoryStatsApi == null)
        {
            ChatCommandRepository.SendMessage(session, "No observatory stats API configured.");
            return;
        }
        
        var request = new HttpRequestMessage(HttpMethod.Get, observatoryStatsApi.Url);
        request.Headers.Add("Authorization", $"{Configuration.ObservatoryApiKey}");

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            ChatCommandRepository.SendMessage(session, $"Failed to get observatory stats: {response.StatusCode} {response.ReasonPhrase}");
            return;
        }

        ChatCommandRepository.SendMessage(session, $"Observatory stats: {await response.Content.ReadAsStringAsync()}\nHas ratelimit headers: {response.Headers.Contains("RateLimit-Limit")}, {response.Headers.Contains("RateLimit-Remaining")}, {response.Headers.Contains("RateLimit-Reset")}");
    }
}