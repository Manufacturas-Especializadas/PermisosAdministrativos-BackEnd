using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployeeById;

public class GetEmployeeByIdQueryHandler
{
    private readonly IEmployeeRepository _employeeRepository;

    public GetEmployeeByIdQueryHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<EmployeeDto> HandleAsync(
        GetEmployeeByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdWithDepartmentAsync(
            query.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("El empleado no existe.");

        return new EmployeeDto(
            employee.Id,
            employee.PayrollNumber,
            employee.FullName,
            employee.DepartmentId,
            employee.Department?.Name ?? "",
            employee.IsActive);
    }
}
