using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Sunrise.API.Extensions;

public static class ProblemResultExtensions
{
    public static IActionResult ToProblemResult(this string detail, HttpStatusCode statusCode, string? title = null)
    {
        return new ObjectResult(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = (int)statusCode
        })
        {
            StatusCode = (int)statusCode
        };
    }
}
