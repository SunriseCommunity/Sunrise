using Microsoft.AspNetCore.Mvc;
using Sunrise.Database.Sqlite;

namespace Sunrise.WebServer.Controllers;

[Controller]
[Route("/web")]
public class SystemController : ControllerBase
{
    private readonly SqliteDatabase _database;

    public SystemController(SqliteDatabase database)
    {
        _database = database;
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
        string[] backgrounds = _database.Files.GetSeasonalBackgroundsTitles();

        // Note: This works because we rewrite ppy.sh requests with Fiddler. Should be improved later.
        string[] seasonalBackgrounds = backgrounds.Select(x => $"https://osu.ppy.sh/static/{x}.jpg").ToArray();

        return Ok(seasonalBackgrounds);
    }

    [HttpGet]
    [Route("check-updates.php")]
    public async Task<IActionResult> CheckUpdates()
    {
        const string staticVersionResponse =
            "[{\"file_version\":\"3\",\"filename\":\"avcodec-51.dll\",\"file_hash\":\"b22bf1e4ecd4be3d909dc68ccab74eec\",\"filesize\":\"4409856\",\"timestamp\":\"2014-08-18 16:16:59\",\"patch_id\":\"1349\",\"url_full\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/f_b22bf1e4ecd4be3d909dc68ccab74eec\",\"url_patch\":\"http:\\/\\/m1.ppy.sh\\/r\\/avcodec-51.dll\\/p_b22bf1e4ecd4be3d909dc68ccab74eec_734e450dd85c16d62c1844f10c6203c0\"}]";

        return Ok(staticVersionResponse);
    }
}