using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Certiva.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class CertivaControllerBase : ControllerBase
{
    protected Guid TenantId =>
        HttpContext.Items.TryGetValue(HttpContextKeys.TenantId, out var t) && t is Guid g ? g : Guid.Empty;

    protected Guid CurrentUserId
    {
        get
        {
            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(ClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    protected string Actor =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst(ClaimNames.Sub)?.Value
        ?? Actors.Unknown;

    protected string RemoteIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    protected IActionResult ToResult<T>(Result<T> result, int successCode = 200)
    {
        if (!result.IsSuccess) return MapError(result.StatusCode, result.Error);
        return successCode switch
        {
            201 => Created(string.Empty, result.Value),
            202 => Accepted(result.Value),
            _ => Ok(result.Value)
        };
    }

    protected IActionResult ToResult(Result result)
    {
        if (!result.IsSuccess) return MapError(result.StatusCode, result.Error);
        return NoContent();
    }

    private IActionResult MapError(int? statusCode, string? error) => statusCode switch
    {
        400 => BadRequest(new { error }),
        401 => Unauthorized(),
        403 => Forbid(),
        404 => NotFound(new { error }),
        409 => Conflict(new { error }),
        422 => UnprocessableEntity(new { error }),
        429 => Problem(error, statusCode: 429),
        _ => Problem(error, statusCode: statusCode ?? 500)
    };
}
