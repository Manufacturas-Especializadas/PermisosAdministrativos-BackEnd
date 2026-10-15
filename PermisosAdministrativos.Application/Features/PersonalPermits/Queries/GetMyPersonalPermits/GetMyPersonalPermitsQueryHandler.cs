using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Common.Models;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetMyPersonalPermits;

public class GetMyPersonalPermitsQueryHandler
{
    private readonly IPersonalPermitRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyPersonalPermitsQueryHandler(
        IPersonalPermitRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<MyPersonalPermitDto>> HandleAsync(
        GetMyPersonalPermitsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
            throw new ValidationException("La página debe ser mayor a 0.");

        if (query.PageSize < 1 || query.PageSize > 100)
            throw new ValidationException("El tamaño de página debe estar entre 1 y 100.");

        var userId = _currentUser.UserId;
        var permits = await _repository.GetPagedByCreatorAsync(
            userId, query.Page, query.PageSize, cancellationToken);
        var totalCount = await _repository.CountByCreatorAsync(userId, cancellationToken);

        var items = permits.Select(x => new MyPersonalPermitDto(
            x.Id,
            x.EmployeeId,
            x.Employee?.PayrollNumber ?? "",
            x.Employee?.FullName ?? "",
            x.PermitDate,
            x.ExitTime,
            x.PermitType,
            x.Reason,
            x.Status,
            x.Status == PermitStatus.Rejected ? x.RejectionReason : null,
            x.HumanResourcesReviewedAt,
            x.ActualExitAt,
            x.CreatedAt)).ToList();

        return new PagedResult<MyPersonalPermitDto>(
            items, query.Page, query.PageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)query.PageSize));
    }
}
