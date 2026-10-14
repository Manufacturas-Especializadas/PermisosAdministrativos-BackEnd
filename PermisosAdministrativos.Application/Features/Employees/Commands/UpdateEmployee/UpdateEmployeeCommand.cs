namespace PermisosAdministrativos.Application.Features.Employees.Commands.UpdateEmployee;

public record UpdateEmployeeCommand(
    int EmployeeId,
    string PayrollNumber,
    string FullName,
    int DepartmentId);
