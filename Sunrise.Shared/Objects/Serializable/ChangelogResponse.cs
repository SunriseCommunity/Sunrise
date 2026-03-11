using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable;

public class ChangelogResponse
{
    [JsonPropertyName("streams")]
    public List<ChangelogStream> Streams { get; set; } = [];
}

public class ChangelogStream
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("latest_build")]
    public ChangelogBuild LatestBuild { get; set; } = new();
}

public class ChangelogBuild
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}