using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPendingPersonalPermits;

public class GetPendingPersonalPermitsQueryHandler
{
    private readonly IPersonalPermitRepository _repository;

    public GetPendingPersonalPermitsQueryHandler(
        IPersonalPermitRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PendingPersonalPermitDto>> HandleAsync(
        GetPendingPersonalPermitsQuery query,
        CancellationToken cancellationToken = default)
    {
        var permits = await _repository.GetPendingAsync(cancellationToken);

        return permits.Select(x => new PendingPersonalPermitDto(
            x.Id,
            x.EmployeeId,
            x.Employee?.PayrollNumber ?? "",
            x.Employee?.FullName ?? "",
            x.PermitDate,
            x.ExitTime,
            x.PermitType,
            x.Reason
        )).ToList();
    }
}