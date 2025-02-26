using osu.Shared;
using osu.Shared.Serialization;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Extensions;
using Sunrise.Server.Managers;
using Sunrise.Server.Utils;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

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
            var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
            User = database.UserService.GetUser(score.UserId).Result;
        }

        if (User == null)
            throw new Exception("User not found");
    }

    public ReplayFile(string filePath)
    {
        Score = new Score();
        using var reader = new ReplayReader(File.Open(filePath, FileMode.Open));

        Score.IsPassed = true;
        var vanillaGameMode = (GameMode)reader.ReadByte();
        Score.OsuVersion = reader.ReadInt32().ToString();
        Score.BeatmapHash = reader.ReadString();
        reader.ReadString(); // Player name  
        Score.ScoreHash = reader.ReadString();
        Score.Count300 = reader.ReadUInt16();
        Score.Count100 = reader.ReadUInt16();
        Score.Count50 = reader.ReadUInt16();
        Score.CountGeki = reader.ReadUInt16();
        Score.CountKatu = reader.ReadUInt16();
        Score.CountMiss = reader.ReadUInt16();
        Score.TotalScore = reader.ReadInt32();
        Score.MaxCombo = reader.ReadUInt16();
        Score.Perfect = reader.ReadBoolean();
        Score.Mods = (Mods)reader.ReadInt32();
        Score.GameMode = vanillaGameMode.EnrichWithMods(Score.Mods);
        reader.ReadString(); // Life graph  
        Score.WhenPlayed = reader.ReadDateTime();
        Data = reader.ReadByteArray(); // Replay data  
        Score.Grade = "X"; // TODO: Implement grade calculation  
        int.TryParse(Score.OsuVersion, out var version);
        if (version >= 20140721)
            Score.Id = (int)reader.ReadInt64();
    }

    private Score Score { get; }
    private User? User { get; }
    private byte[] Data { get; }

    public Score GetScore()
    {
        return Score;
    }

    public byte[] GetReplayData()
    {
        return Data;
    }

    public async Task<MemoryStream> ReadReplay()
    {
        var memoryStream = new MemoryStream();
        await using var writer = new SerializationWriter(memoryStream);

        // My gratefulness to rxhddt and mrflashstudio for having open-source replay parsers. 

        writer.Write((byte)Score.GameMode.ToVanillaGameMode());
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
        writer.Write((int)Score.TotalScore);
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
        var beatmapSet = session != null
            ? await BeatmapManager.GetBeatmapSet(session, beatmapHash: Score.BeatmapHash)
            : null;

        return
            $"{User?.Username} - {beatmapSet?.Artist} - {beatmapSet?.Title} [{Score.BeatmapId}] ({Score.WhenPlayed:yyyy-MM-dd}) {Score.GameMode}.osr";
    }

    private string GetReplayHash()
    {
        return
            $"{Score.Count100 + Score.Count300}p{Score.Count50}o{Score.CountGeki}o{Score.CountKatu}t{Score.CountMiss}a{Score.BeatmapHash}r{Score.MaxCombo}e{Score.Perfect}y{User?.Username}o{Score.TotalScore}u0{(int)Score.Mods}{Score.IsPassed}"
                .ToHash();
    }
}