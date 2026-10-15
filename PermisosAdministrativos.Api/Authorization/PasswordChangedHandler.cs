using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using PermisosAdministrativos.Infrastructure.Identity;

namespace PermisosAdministrativos.Api.Authorization;

public sealed class PasswordChangedHandler
    : AuthorizationHandler<PasswordChangedRequirement>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PasswordChangedHandler(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PasswordChangedRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var user = await _userManager.GetUserAsync(context.User);

        if (user is null)
            return;

        if (!user.MustChangePassword)
        {
            context.Succeed(requirement);
        }
    } 
}