namespace PermisosAdministrativos.Application.DTOs;

public record UserDto(
    string Id,
    string UserName,
    int? EmployeeId,
    string? PayrollNumber,
    string? EmployeeName,
    bool IsActive,
    List<string> Roles);