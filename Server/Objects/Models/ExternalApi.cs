using Sunrise.Server.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Server.Objects.Models;

[Table("external_api")]
public class ExternalApi
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }

    [Column(DataTypes.Nvarchar, 256, false)]
    public string Url { get; set; } = string.Empty;

    [Column(DataTypes.Int, false)]
    public ApiServer Server { get; set; }

    [Column(DataTypes.Int, false)]
    public ApiType Type { get; set; }

    [Column(DataTypes.Int, false)]
    public int Priority { get; set; }

    [Column(DataTypes.Int, false)]
    public int NumberOfRequiredArgs { get; set; }
    
    public ExternalApi Fill(ApiType type, ApiServer server, string url, int priority, int numberOfRequiredArgs)
    {
        Type = type;
        Server = server;
        Url = url;
        Priority = priority;
        NumberOfRequiredArgs = numberOfRequiredArgs;
        return this;
    }
}