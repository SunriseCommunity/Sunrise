using osu.Shared.Serialization;
using Sunrise.Server.Data;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Managers;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Objects;

public class ReplayFile
{
    public ReplayFile(Score score, byte[] rawReplay, User? user = null)
    {
        Score = score;
        User = user;
        Data = rawReplay;

        if (user == null)
        {
            var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
            User = database.GetUser(id: score.UserId).Result;
        }

        if (User == null)
            throw new Exception("User not found");
    }

    private Score Score { get; }
    private User? User { get; }
    private byte[] Data { get; }

    public async Task<MemoryStream> ReadReplay()
    {
        var memoryStream = new MemoryStream();
        await using var writer = new SerializationWriter(memoryStream);

        // My gratefulness to rxhddt and mrflashstudio for having open-source replay parsers. 

        writer.Write((byte)Score.GameMode);
        writer.Write(int.TryParse(Score.OsuVersion, out var osuVersion) ? osuVersion : 20140721);
        writer.Write(Score.BeatmapHash);
        writer.Write(User?.Username ?? "Unknown");
        writer.Write(GetReplayHash());
        writer.Write((short)Score.Count300);
        writer.Write((short)Score.Count100);
        writer.Write((short)Score.Count50);
        writer.Write((short)Score.CountGeki);
        writer.Write((short)Score.CountKatu);
        writer.Write((short)Score.CountMiss);
        writer.Write(Score.TotalScore);
        writer.Write((short)Score.MaxCombo);
        writer.Write(Score.Perfect);
        writer.Write((int)Score.Mods);
        writer.Write(""); // Life frames graph. But I think it's impossible to get from data we have. Could be wrong.
        writer.Write(Score.WhenPlayed);
        writer.Write(Data);
        writer.Write((long)Score.Id);

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }

    public async Task<string> GetFileName(BaseSession? session)
    {
        var beatmapSet = session != null ? await BeatmapManager.GetBeatmapSet(session, beatmapHash: Score.BeatmapHash) : null;

        return $"{User?.Username} - {beatmapSet?.Artist} - {beatmapSet?.Title} [{Score.BeatmapId}] ({Score.WhenPlayed:yyyy-MM-dd}) {Score.GameMode}.osr";
    }

    private string GetReplayHash()
    {
        return $"{Score.Count100 + Score.Count300}p{Score.Count50}o{Score.CountGeki}o{Score.CountKatu}t{Score.CountMiss}a{Score.BeatmapHash}r{Score.MaxCombo}e{Score.Perfect}y{User?.Username}o{Score.TotalScore}u0{(int)Score.Mods}{Score.IsPassed}".ToHash();
    }
}