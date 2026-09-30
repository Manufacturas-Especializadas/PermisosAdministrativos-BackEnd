using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Application.Features.Users.Queries.GetUsers;

public class GetUsersQueryHandler
{
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(
        IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<List<UserDto>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        return _identityService.GetUsersAsync(cancellationToken);
    }
}