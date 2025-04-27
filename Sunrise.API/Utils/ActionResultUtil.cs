using Microsoft.AspNetCore.Mvc;
using Sunrise.API.Serializable.Response;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.API.Utils;

public static class ActionResultUtil
{
    public static IActionResult ActionErrorResult(ErrorMessage error)
    {
        return new ObjectResult(new ErrorResponse(error.Message))
        {
            StatusCode = (int)error.Status
        };
    }
}