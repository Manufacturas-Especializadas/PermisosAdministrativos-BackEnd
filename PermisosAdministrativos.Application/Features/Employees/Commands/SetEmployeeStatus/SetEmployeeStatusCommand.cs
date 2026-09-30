namespace PermisosAdministrativos.Application.Features.Employees.Commands.SetEmployeeStatus;

public record SetEmployeeStatusCommand(
    int EmployeeId,
    bool IsActive);