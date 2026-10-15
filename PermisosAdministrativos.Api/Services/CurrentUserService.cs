using System.Security.Claims;
using PermisosAdministrativos.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Infrastructure.Identity;

namespace PermisosAdministrativos.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public async Task<int?> GetEmployeeIdAsync(CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        var user = await _userManager.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.EmployeeId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No se encontró el usuario autenticado.");

        return user.EmployeeId;
    }

    public string UserId
    {
        get
        {
            var userId = _httpContextAccessor.HttpContext?
                .User
                .FindFirst(ClaimTypes.NameIdentifier)?
                .Value;

            return userId
                ?? throw new InvalidOperationException(
                    "No hay un usuario autenticado.");
        }
    }
}
