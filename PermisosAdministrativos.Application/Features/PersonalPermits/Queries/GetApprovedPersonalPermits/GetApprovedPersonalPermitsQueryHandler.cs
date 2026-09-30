using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetApprovedPersonalPermits;

public class GetApprovedPersonalPermitsQueryHandler
{
    private readonly IPersonalPermitRepository _repository;
    private readonly IBusinessDateService _businessDateService;

    public GetApprovedPersonalPermitsQueryHandler(
        IPersonalPermitRepository repository,
        IBusinessDateService businessDateService)
    {
        _repository = repository;
        _businessDateService = businessDateService;
    }

    public async Task<List<ApprovedPersonalPermitDto>> HandleAsync(
        GetApprovedPersonalPermitsQuery query,
        CancellationToken cancellationToken = default)
    {
        var today = _businessDateService.Today;

        var permits = await _repository.GetApprovedAsync(
            today,
            cancellationToken);

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