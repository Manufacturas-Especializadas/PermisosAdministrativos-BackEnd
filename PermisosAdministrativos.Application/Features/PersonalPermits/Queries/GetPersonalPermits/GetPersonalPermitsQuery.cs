using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPersonalPermits;

public record GetPersonalPermitsQuery(
    int? EmployeeId,
    PermitStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int PageSize);