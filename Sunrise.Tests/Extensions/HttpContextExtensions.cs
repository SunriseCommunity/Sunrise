using System.Net.Http.Json;
using Sunrise.Shared.Application;

namespace Sunrise.Tests.Extensions;

public static class HttpContextExtensions
{

    public static async Task<T?> ReadFromJsonAsyncWithAppConfig<T>(this HttpContent content)
    {
        var options = Configuration.SystemTextJsonOptions;
        return await content.ReadFromJsonAsync<T>(options);
    }
}