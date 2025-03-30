using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Users;

namespace Sunrise.API.Serializable.Response;

public class GradesResponse
{
    [JsonConstructor]
    public GradesResponse() { }

    public GradesResponse(UserGrades grades)
    {
        CountXH = grades.CountXH;
        CountX = grades.CountX;
        CountSH = grades.CountSH;
        CountS = grades.CountS;
        CountA = grades.CountA;
        CountB = grades.CountB;
        CountC = grades.CountC;
        CountD = grades.CountD;
    }

    [JsonPropertyName("count_xh")]
    public int CountXH { get; set; }

    [JsonPropertyName("count_x")]
    public int CountX { get; set; }

    [JsonPropertyName("count_sh")]
    public int CountSH { get; set; }

    [JsonPropertyName("count_s")]
    public int CountS { get; set; }

    [JsonPropertyName("count_a")]
    public int CountA { get; set; }

    [JsonPropertyName("count_b")]
    public int CountB { get; set; }

    [JsonPropertyName("count_c")]
    public int CountC { get; set; }

    [JsonPropertyName("count_d")]
    public int CountD { get; set; }
}