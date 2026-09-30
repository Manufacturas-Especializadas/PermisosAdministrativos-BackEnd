using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Application.DTOs;

public record PendingPersonalPermitDto(
    int Id,
    int EmployeeId,
    string PayrollNumber,
    string EmployeeName,
    DateOnly PermitDate,
    TimeOnly ExitTime,
    PersonalPermitType PermitType,
    string Reason);