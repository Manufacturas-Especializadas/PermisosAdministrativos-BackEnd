using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetMyPersonalPermits;

public record MyPersonalPermitDto(
    int Id,
    int EmployeeId,
    string PayrollNumber,
    string EmployeeName,
    DateOnly PermitDate,
    TimeOnly ExitTime,
    PersonalPermitType PermitType,
    string Reason,
    PermitStatus Status,
    string? RejectionReason,
    DateTime? HumanResourcesReviewedAt,
    DateTime? ActualExitAt,
    DateTime CreatedAt);
