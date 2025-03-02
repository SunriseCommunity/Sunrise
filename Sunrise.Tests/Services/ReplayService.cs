using Microsoft.AspNetCore.Http;
using Sunrise.Shared.Objects;
using Sunrise.Tests.Services.Mock;

namespace Sunrise.Tests.Services;

public class ReplayService
{
    private readonly MockService _mocker = new();

    public IFormFile GenerateReplayFormFile(int length = 1024)
    {
        return GenerateReplayFormFile(new byte[length], $"{_mocker.GetRandomString(6)}.osr");
    }

    public IFormFile GenerateReplayFormFile(ReplayFile replay)
    {
        var buffer = replay.GetReplayData();

        return GenerateReplayFormFile(buffer, "replay.osr");
    }

    private IFormFile GenerateReplayFormFile(byte[] buffer, string fileName)
    {
        IFormFile formFile = new FormFile(new MemoryStream(buffer), 0, buffer.Length, "data", fileName);
        return formFile;
    }
}