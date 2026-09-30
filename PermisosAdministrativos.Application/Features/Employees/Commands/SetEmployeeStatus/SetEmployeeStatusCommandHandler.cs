using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Commands.SetEmployeeStatus;

public class SetEmployeeStatusCommandHandler
{
    private readonly IEmployeeRepository _employeeRepository;

    public SetEmployeeStatusCommandHandler(
        IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task HandleAsync(
        SetEmployeeStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(
            command.EmployeeId,
            cancellationToken)
            ?? throw new NotFoundException(
                    "El empleado no existe.");

        employee.IsActive = command.IsActive;

        await _employeeRepository.UpdateAsync(
            employee,
            cancellationToken);
    }
}