using System.Net;
using System.Security.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.API.Utils;

public static class ActionResultUtil
{
    public static IActionResult ActionErrorResult(ErrorMessage error)
    {
        var exception = error.Status switch
        {
            HttpStatusCode.BadRequest => new BadHttpRequestException(error.Message),
            HttpStatusCode.Forbidden => new AuthenticationException(error.Message),
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException(error.Message),
            HttpStatusCode.RequestTimeout => new TimeoutException(error.Message),
            HttpStatusCode.NotFound => new KeyNotFoundException(error.Message),
            _ => new Exception(error.Message)
        };

        throw exception;
    }
}