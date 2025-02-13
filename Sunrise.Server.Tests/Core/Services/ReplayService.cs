using Microsoft.AspNetCore.Http;
using Sunrise.Server.Objects;
using Sunrise.Server.Tests.Core.Services.Mock;

namespace Sunrise.Server.Tests.Core.Services;

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