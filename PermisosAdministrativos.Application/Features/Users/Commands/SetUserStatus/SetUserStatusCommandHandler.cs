using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Application.Features.Users.Commands.SetUserStatus;

public class SetUserStatusCommandHandler
{
    private readonly IIdentityService _identityService;

    public SetUserStatusCommandHandler(
        IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task HandleAsync(
        SetUserStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        await _identityService.SetUserActiveStatusAsync(
            command.UserId,
            command.IsActive);
    }
}
