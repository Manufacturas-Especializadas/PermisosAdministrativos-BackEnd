using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Common.Models;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPersonalPermits;

public class GetPersonalPermitsQueryHandler
{
    private readonly IPersonalPermitRepository _repository;

    public GetPersonalPermitsQueryHandler(
        IPersonalPermitRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<PersonalPermitDto>> HandleAsync(
        GetPersonalPermitsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
            throw new ValidationException(
                "La página debe ser mayor a 0.");

        if (query.PageSize < 1 || query.PageSize > 100)
            throw new ValidationException(
                "El tamaño de página debe estar entre 1 y 100.");

        var permits = await _repository.GetPagedAsync(
            query.EmployeeId,
            query.Status,
            query.FromDate,
            query.ToDate,
            query.Page,
            query.PageSize,
            cancellationToken);

        var totalCount = await _repository.CountAsync(
            query.EmployeeId,
            query.Status,
            query.FromDate,
            query.ToDate,
            cancellationToken);

        var items = permits.Select(x => new PersonalPermitDto(
            x.Id,
            x.EmployeeId,
            x.Employee?.PayrollNumber ?? "",
            x.Employee?.FullName ?? "",
            x.PermitDate,
            x.ExitTime,
            x.PermitType,
            x.Reason,
            x.Status))
            .ToList();

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)query.PageSize);

        return new PagedResult<PersonalPermitDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);
    }
}