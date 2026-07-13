using System.Diagnostics;
using System.Globalization;
using System.Text;
using CSharpFunctionalExtensions;
using osu.Shared;
using Serilog;
using SharpCompress.Compressors.LZMA;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Processing.Utils;

// TODO: This validation only checks the replay file structure, but it doesn't compare score objects to the submitted data (e.g. take replay's counts and compare with counts send in the request)
// Ideally we should have some kind of background processor to validate this info, since it will require loading .osu file. Maybe for the anti-cheat update?

public static class ReplayValidationUtil
{
    private const int HeaderSize = 13;
    private const int MaxCompressedBytes = 4 * 1024 * 1024;
    private const int MaxDecompressedBytes = 16 * 1024 * 1024;
    private const int TerminalRngSeedFrameDelta = -12345;
    private static readonly TimeSpan MaxDecodeTime = TimeSpan.FromSeconds(2);

    public static UnitResult<ScoreProcessingError> Validate(byte[] replay, Beatmap beatmap, Score score)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (replay.Length is < HeaderSize or > MaxCompressedBytes)
                return Failure("Replay compressed size is outside allowed bounds");

            var outputSize = BitConverter.ToInt64(replay, 5);
            if (outputSize is <= 0 or > MaxDecompressedBytes)
                return Failure("Replay decompressed size is outside allowed bounds");

            using var input = new MemoryStream(replay, HeaderSize, replay.Length - HeaderSize, false);
            using var decoder = LzmaStream.Create(replay[..5], input, replay.Length - HeaderSize, outputSize);
            using var output = new MemoryStream((int)outputSize);
            decoder.CopyTo(output);
            if (output.Length != outputSize || stopwatch.Elapsed > MaxDecodeTime)
                return Failure("Replay decompression exceeded its size or time limit");

            var text = Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
            long duration = 0;
            var gameplayFrames = 0;
            var terminalFrameSeen = false;

            foreach (var rawFrame in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var fields = rawFrame.Split('|');
                if (fields.Length != 4 || !long.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta) ||
                    !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                    !float.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                    !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var keys))
                    return Failure("Replay contains a malformed frame");

                if (delta == TerminalRngSeedFrameDelta)
                {
                    if (terminalFrameSeen || x != 0 || y != 0) return Failure("Replay contains an invalid RNG seed frame");

                    terminalFrameSeen = true;
                    continue;
                }

                if (terminalFrameSeen || !float.IsFinite(x) || !float.IsFinite(y))
                    return Failure("Replay contains invalid frame values");

                if (keys is < 0 or > 31)
                    return Failure("Replay contains an invalid key mask");

                duration = checked(duration + delta);
                gameplayFrames++;
            }

            if (gameplayFrames == 0)
                return Failure("Replay contains no gameplay frames");

            var rate = score.Mods.HasFlag(Mods.DoubleTime) || score.Mods.HasFlag(Mods.Nightcore) ? 1.5 :
                score.Mods.HasFlag(Mods.HalfTime) ? 0.75 : 1;
            var maximumDuration = beatmap.TotalLength * 1000d / rate + 30_000;
            if (duration < 0 || duration > maximumDuration || score.TimeElapsed > 0 && Math.Abs(duration - score.TimeElapsed) > 60_000)
                return Failure("Replay duration is inconsistent with the submitted play");

            Log.Information("Validated replay for user {UserId}: compressed={CompressedBytes}, decompressed={DecompressedBytes}, frames={Frames}, elapsedMs={ElapsedMs}",
                score.UserId,
                replay.Length,
                output.Length,
                gameplayFrames,
                stopwatch.ElapsedMilliseconds);
            return UnitResult.Success<ScoreProcessingError>();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Replay validation failed for user {UserId} after {ElapsedMs}ms", score.UserId, stopwatch.ElapsedMilliseconds);
            return Failure("Replay is corrupt or could not be decoded");
        }
    }

    private static UnitResult<ScoreProcessingError> Failure(string message)
    {
        return new ScoreProcessingError(ScoreProcessingErrorCode.InvalidReplay, message).ToUnit();
    }
}