namespace PermisosAdministrativos.Api.Contracts.Users;

public record CreateUserRequest(
    string UserName,
    string Password,
    int? EmployeeId,
    string Role);