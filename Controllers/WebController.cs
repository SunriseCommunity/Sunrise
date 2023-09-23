using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Sunrise.Helpers;
using Sunrise.Objects;
using Sunrise.Services;

namespace Sunrise.Controllers;

[Controller]
[Route("[controller]")]
public class WebController : ControllerBase
{
    private readonly PlayerRepository _playerRepository;
    private const string STABLEKEY = "osu!-scoreburgr---------";

    public WebController(PlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }
    
    [HttpPost]
    [Route("osu-submit-modular-selector.php")]
    public async Task<IActionResult> Submit()
    {

        var scoreEncoded = Request.Form["score"];
        var osuver = Request.Form["osuver"];
        var iv = Request.Form["iv"];
        var pass = Request.Form["pass"];

        string beatmapString;

        if (scoreEncoded == string.Empty || osuver == string.Empty || iv == string.Empty || pass == string.Empty)
            return BadRequest("error: beatmap");

        if (osuver != string.Empty)
            beatmapString = ScoreDecoder.Decode(scoreEncoded, iv, STABLEKEY, osuver);
        else
        //didnt test fallback version, so i don't know will it work. (95% it won't)
            beatmapString = ScoreDecoder.Decode(
                scoreEncoded!, iv!, Request.Form["AES"], string.Empty);

        var score = new Score();
        score.ParseScore(beatmapString);
        
        //bad code :( idk how i can make this better 
        //var player = _playerRepository.GetPlayerByUsername(score.Username);
        

        Console.WriteLine(score.ToString());
        
        
        return Ok();
    }
    
    [HttpGet]
    public IActionResult GetIndex()
    {
        return Ok("Hello world");
    }

    [HttpPost]
    [Route("osu-error.php")]
    public IActionResult OsuError()
    {
        Console.WriteLine("error");
        return Ok();
    }

    [HttpGet]
    [Route("bancho_connect.php")]
    public IActionResult BanchoConnect(string v, string u, string h, string fail = "", string fx = "", string ch = "", string retry = "")
    {
        return Ok("fi");
    }
    
    [HttpPost]
    [Route("osu-session.php")]
    public IActionResult OsuConnect()
    {
        return Ok();
    }
    
    [HttpGet]
    [Route("osu-getseasonal.php")]
    public async Task<IActionResult> GetSeasonal()
    {
        return Ok("nothing so see her :l");
    }
}