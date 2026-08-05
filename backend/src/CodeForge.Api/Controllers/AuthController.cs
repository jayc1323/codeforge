using CodeForge.Api.Auth;
using CodeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CodeForge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<ApplicationUser> userManager, JwtTokenService tokenService) : ControllerBase
{
    public sealed record RegisterRequest(string Email, string Password);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record AuthResponse(string Token, string Email, DateTimeOffset ExpiresAt);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var (token, expiresAt) = tokenService.CreateToken(user.Id, user.Email);
        return Ok(new AuthResponse(token, user.Email, expiresAt));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        // Same response whether the email is unknown or the password is wrong:
        // don't leak which emails are registered.
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { error = "Invalid email or password." });

        var (token, expiresAt) = tokenService.CreateToken(user.Id, user.Email!);
        return Ok(new AuthResponse(token, user.Email!, expiresAt));
    }
}
