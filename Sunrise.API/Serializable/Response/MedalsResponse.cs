using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace Sunrise.API.Serializable.Response;

public class MedalsResponse(List<UserMedals> userMeals, List<Medal> medals)
{
    [JsonPropertyName("hush_hush")]
    public Category HushHushCategory => new()
    {
        Medals = medals.Where(x => x.Category == MedalCategory.HushHush)
            .Select(x => new UserMedalResponse(x, userMeals.FirstOrDefault(y => y.MedalId == x.Id))).ToList()
    };

    [JsonPropertyName("beatmap_hunt")]
    public Category BeatmapHuntCategory => new()
    {
        Medals = medals.Where(x => x.Category == MedalCategory.BeatmapHunt).Select(x =>
            new UserMedalResponse(x, userMeals.FirstOrDefault(y => y.MedalId == x.Id))).ToList()
    };

    [JsonPropertyName("mod_introduction")]
    public Category ModIntroductionCategory => new()
    {
        Medals = medals.Where(x => x.Category == MedalCategory.ModIntroduction).Select(x =>
            new UserMedalResponse(x, userMeals.FirstOrDefault(y => y.MedalId == x.Id))).ToList()
    };

    [JsonPropertyName("skill")]
    public Category SkillCategory => new()
    {
        Medals = medals.Where(x => x.Category == MedalCategory.Skill)
            .Select(x => new UserMedalResponse(x, userMeals.FirstOrDefault(y => y.MedalId == x.Id))).ToList()
    };

    public class Category
    {
        [JsonPropertyName("medals")]
        [SwaggerSchema(ReadOnly = true)]
        public List<UserMedalResponse> Medals { get; set; }
    }
}