namespace PermisosAdministrativos.Application.Features.Users.Commands.CreateUser;

public record CreateUserCommand(
    string UserName,
    string Password,
    int? EmployeeId,
    string Role);