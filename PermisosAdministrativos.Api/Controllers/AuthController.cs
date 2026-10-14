using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Application.DTOs.Auth;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Infrastructure.Identity;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUserService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        return Ok(new
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserName = User.Identity?.Name,
            Roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                .Select(x => x.Value),
            MustChangePassword = user.MustChangePassword
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();

        var mustChangePassword = user.MustChangePassword;
        var succeeded = false;
        try
        {
            // Identity persists the flag with the new password hash and security stamp.
            user.MustChangePassword = false;
            var result = await _userManager.ChangePasswordAsync(
                user, request.CurrentPassword, request.NewPassword);
            succeeded = result.Succeeded;

            if (!result.Succeeded)
            {
                return BadRequest(new ValidationProblemDetails(
                    result.Errors.GroupBy(error => error.Code).ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.Description).ToArray()))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "No se pudo cambiar la contraseña."
                });
            }
        }
        finally
        {
            if (!succeeded)
                user.MustChangePassword = mustChangePassword;
        }

        await _signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        if (User.Identity?.IsAuthenticated != true ||
            !User.HasClaim(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier))
            return null;

        return await _userManager.FindByIdAsync(_currentUserService.UserId);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);

        if (user is null || !user.IsActive)
            return Unauthorized("Usuario o contraseña incorrectos.");

        var result = await _signInManager.PasswordSignInAsync(
            request.UserName,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
            return Unauthorized("Usuario o contraseña incorrectos.");

        return Ok();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return NoContent();
    }
}
