using System.Net.Http.Headers;
using Sunrise.Server.API.Serializable.Response;

namespace Sunrise.Server.Tests.Core.Utils;

public static class RequestUtil
{
    public static HttpClient UseClient(this HttpClient client,string appDomain)
    {
        client.BaseAddress = new Uri($"https://{appDomain}.sunrise.com");
        return client;
    }
    
    public static HttpClient UseUserAuthToken(this HttpClient client, TokenResponse tokens)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        
        return client;
    }

    public static HttpClient UseUserIp(this HttpClient client, string ip)
    {
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        
        return client;
    }
}