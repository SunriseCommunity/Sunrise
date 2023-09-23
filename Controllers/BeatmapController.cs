using System.Net;
using System.Text.Json;
using HOPEless.Bancho.Objects;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Objects.Serializable;
using Sunrise.Objects.Serializable.ChimuApi;

namespace Sunrise.Controllers;

[Controller]
[Route("/")]
public class BeatmapController : ControllerBase
{
    //doesn't work with mods.
    [Route("difficulty-rating")]
    public async Task<ActionResult> GetDifficulty([FromBody] DifficultyRequest request)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.chimu.moe/v1/map/"),
        };
        
        var response = await client.GetAsync(request.BeatmapId.ToString());

        if (response.StatusCode != HttpStatusCode.OK)
            return BadRequest("Error fetching beatmap information.");
        
        var beatmap = await JsonSerializer.DeserializeAsync<ChimuBeatmap>(await response.Content.ReadAsStreamAsync());
        
        
        return Ok(beatmap.DifficultyRating);
    }
}