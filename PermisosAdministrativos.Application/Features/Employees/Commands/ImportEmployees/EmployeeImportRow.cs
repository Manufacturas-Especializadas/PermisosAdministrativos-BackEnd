namespace PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;

public record EmployeeImportRow(
    int RowNumber,
    string PayrollNumber,
    string FullName,
    string DepartmentName);