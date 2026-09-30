using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetApprovedPersonalPermits;

public record ApprovedPersonalPermitDto(
    int Id,
    int EmployeeId,
    string PayrollNumber,
    string EmployeeName,
    DateOnly PermitDate,
    TimeOnly ExitTime,
    PersonalPermitType PermitType,
    string Reason);