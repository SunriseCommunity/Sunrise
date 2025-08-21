using Microsoft.AspNetCore.Mvc;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.API.Utils;

public static class ActionResultUtil
{
    public static IActionResult ActionErrorResult(ErrorMessage error)
    {
        var problemDetails = new ProblemDetails
        {
            Detail = error.Message,
            Status = (int)error.Status
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
}