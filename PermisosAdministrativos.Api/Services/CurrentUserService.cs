using System.Security.Claims;
using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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