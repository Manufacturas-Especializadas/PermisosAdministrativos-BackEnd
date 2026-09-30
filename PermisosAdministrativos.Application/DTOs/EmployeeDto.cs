namespace PermisosAdministrativos.Application.DTOs;

public record EmployeeDto(
    int Id,
    string PayrollNumber,
    string FullName,
    int DepartmentId,
    string DepartmentName,
    bool IsActive);