using Sunrise.Shared.Enums;

namespace Sunrise.Shared.Objects;

public class ExternalApi(ApiType type, ApiServer server, string url, int priority, int numberOfRequiredArgs)
{
    public string Url { get; private set; } = url;
    public ApiServer Server { get; private set; } = server;
    public ApiType Type { get; private set; } = type;
    public int Priority { get; private set; } = priority;
    public int NumberOfRequiredArgs { get; private set; } = numberOfRequiredArgs;
}