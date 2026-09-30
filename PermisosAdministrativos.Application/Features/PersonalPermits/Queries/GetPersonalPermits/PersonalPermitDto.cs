using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPersonalPermits;

public record PersonalPermitDto(
    int Id,
    int EmployeeId,
    string PayrollNumber,
    string EmployeeName,
    DateOnly PermitDate,
    TimeOnly ExitTime,
    PersonalPermitType PermitType,
    string Reason,
    PermitStatus Status);