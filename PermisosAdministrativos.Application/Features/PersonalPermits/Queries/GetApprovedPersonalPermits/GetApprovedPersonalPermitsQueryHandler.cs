using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetApprovedPersonalPermits;

public class GetApprovedPersonalPermitsQueryHandler
{
    private readonly IPersonalPermitRepository _repository;

    public GetApprovedPersonalPermitsQueryHandler(
        IPersonalPermitRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ApprovedPersonalPermitDto>> HandleAsync(
        GetApprovedPersonalPermitsQuery query,
        CancellationToken cancellationToken = default)
    {
        var permits = await _repository.GetApprovedAsync(cancellationToken);

        return permits.Select(x => new ApprovedPersonalPermitDto(
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