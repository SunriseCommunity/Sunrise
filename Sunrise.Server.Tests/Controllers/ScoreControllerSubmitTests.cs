using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Sunrise.Tests.Abstracts;
using Sunrise.Tests.Utils;

namespace Sunrise.Server.Tests.Controllers;

[Collection("Integration tests collection")]
public class ScoreControllerSubmitTests(IntegrationDatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const string OsuVersion = "20200101";
    private const string StableKey = "osu!-scoreburgr---------";

    [Fact]
    public async Task TestSubmitScoreWithInvalidPasshashReturnsPassErrorWithoutSavingScore()
    {
        var client = App.CreateClient().UseClient("osu");
        var user = await CreateTestUser();
        var scoreCountBefore = await Database.DbContext.Scores.CountAsync();

        var response = await client.PostAsync("/web/osu-submit-modular-selector.php",
            CreateSubmissionForm(user.Username, "invalid-passhash"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("error: pass", await response.Content.ReadAsStringAsync());
        Assert.Equal(scoreCountBefore, await Database.DbContext.Scores.CountAsync());
    }

    [Fact]
    public async Task TestSubmitScoreWithValidCredentialsButNoSessionReturnsPassErrorWithoutSavingScore()
    {
        var client = App.CreateClient().UseClient("osu");
        var user = await CreateTestUser();
        var scoreCountBefore = await Database.DbContext.Scores.CountAsync();

        var response = await client.PostAsync("/web/osu-submit-modular-selector.php",
            CreateSubmissionForm(user.Username, user.Passhash));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("error: pass", await response.Content.ReadAsStringAsync());
        Assert.Equal(scoreCountBefore, await Database.DbContext.Scores.CountAsync());
    }

    private static FormUrlEncodedContent CreateSubmissionForm(string username, string passhash)
    {
        var iv = new byte[32];
        Random.Shared.NextBytes(iv);

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["pass"] = passhash,
            ["bmk"] = "test-beatmap-hash",
            ["st"] = "0",
            ["ft"] = "0",
            ["osuver"] = OsuVersion,
            ["s"] = Encrypt("test-client-hash", iv),
            ["iv"] = Convert.ToBase64String(iv),
            ["score"] = Encrypt($"0:{username}", iv),
            ["x"] = ""
        });
    }

    private static string Encrypt(string value, byte[] iv)
    {
        var key = Encoding.Default.GetBytes($"{StableKey}{OsuVersion}");
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new RijndaelEngine(256)), new Pkcs7Padding());
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));

        var input = Encoding.UTF8.GetBytes(value);
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var length = cipher.ProcessBytes(input, 0, input.Length, output, 0);
        length += cipher.DoFinal(output, length);
        Array.Resize(ref output, length);

        return Convert.ToBase64String(output);
    }
}
