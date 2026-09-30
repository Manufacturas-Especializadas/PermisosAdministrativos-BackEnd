using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CreatePersonalPermit;

public record CreatePersonalPermitCommand(
    int EmployeeId,
    DateOnly PermitDate,
    TimeOnly ExitTime,
    PersonalPermitType PermitType,
    string Reason);