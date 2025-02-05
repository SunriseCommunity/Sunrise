namespace Sunrise.Server.Tests.Utils;

public static class RequestUtil
{
    public static HttpClient UseClient(this HttpClient client,string appDomain)
    {
        client.BaseAddress = new Uri($"https://{appDomain}.sunrise.com");
        return client;
    }
}