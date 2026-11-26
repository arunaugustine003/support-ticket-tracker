using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SupportTracker.Security;

namespace SupportTracker.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public ActionResult<SessionInfo> Login(LoginInput input)
    {
        var security = HttpContext.RequestServices.GetRequiredService<TicketSecurity>();
        if (!security.CheckCredentials(input.Username, input.Password))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid username or password." });

        var (token, expiresAt) = security.IssueToken();
        var cookie = TicketSecurity.SessionCookie();
        cookie.Expires = new DateTimeOffset(expiresAt);
        Response.Cookies.Append(TicketSecurity.CookieName, token, cookie);
        return new SessionInfo(expiresAt);
    }

    [HttpGet("session")]
    public ActionResult<SessionInfo> Session()
    {
        var expiration = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (!long.TryParse(expiration, CultureInfo.InvariantCulture, out var seconds))
            return Unauthorized();

        return new SessionInfo(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(TicketSecurity.CookieName, TicketSecurity.SessionCookie());
        return NoContent();
    }
}

public sealed class LoginInput
{
    [Required, StringLength(128)]
    public string Username { get; init; } = "";

    [Required, StringLength(256)]
    public string Password { get; init; } = "";
}

public sealed record SessionInfo(DateTime ExpiresAt);
