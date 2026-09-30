using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Common.Models;
using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployees;

public class GetEmployeesQueryHandler
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeesQueryHandler(
        IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<PagedResult<EmployeeDto>> HandleAsync(
        GetEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page < 1)
            throw new ValidationException("La página debe ser mayor a 0.");

        if (query.PageSize < 1 || query.PageSize > 100)
            throw new ValidationException(
                "El tamaño de página debe estar entre 1 y 100.");

        var employees = await _employeeRepository.GetPagedAsync(
            query.Search,
            query.IsActive,
            query.DepartmentId,
            query.Page,
            query.PageSize,
            cancellationToken);

        var totalCount = await _employeeRepository.CountAsync(
            query.Search,
            query.IsActive,
            query.DepartmentId,
            cancellationToken);

        var items = employees.Select(x => new EmployeeDto(
            x.Id,
            x.PayrollNumber,
            x.FullName,
            x.DepartmentId,
            x.Department?.Name ?? "",
            x.IsActive))
            .ToList();

        var totalPages = (int)Math.Ceiling(
            totalCount / (double)query.PageSize);

        return new PagedResult<EmployeeDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);
    }
}