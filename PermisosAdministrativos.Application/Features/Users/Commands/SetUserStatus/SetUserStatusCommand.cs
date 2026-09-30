namespace PermisosAdministrativos.Application.Features.Users.Commands.SetUserStatus;

public record SetUserStatusCommand(
    string UserId,
    bool IsActive);
