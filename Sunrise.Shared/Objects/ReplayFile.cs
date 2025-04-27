using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using osu.Shared.Serialization;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Objects;

public class ReplayFile
{
    public ReplayFile(Score score, byte[] rawReplay, User? user = null)
    {
        Score = score;
        User = user;
        Data = rawReplay;

        if (user == null)
        {
            using var scope = ServicesProviderHolder.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            User = database.Users.GetUser(score.UserId).Result;
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

    public async Task<string> GetFileName(BaseSession session)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var beatmapService = scope.ServiceProvider.GetRequiredService<BeatmapService>();

        var beatmapSetResult = await beatmapService.GetBeatmapSet(session, beatmapHash: Score.BeatmapHash);
        if (beatmapSetResult.IsFailure)
            return $"{User?.Username} - Unknown - Unknown [{Score.BeatmapId}] ({Score.WhenPlayed:yyyy-MM-dd}) {Score.GameMode}.osr";

        var beatmapSet = beatmapSetResult.Value;

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