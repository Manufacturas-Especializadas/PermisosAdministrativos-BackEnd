namespace PermisosAdministrativos.Api.Contracts.Employees;

public record UpdateEmployeeRequest(
    string PayrollNumber,
    string FullName,
    int DepartmentId);
