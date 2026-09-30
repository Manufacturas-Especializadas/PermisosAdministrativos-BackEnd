namespace PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;

public record CreateEmployeeCommand(
    string PayrollNumber,
    string FullName,
    int DepartmentId);
