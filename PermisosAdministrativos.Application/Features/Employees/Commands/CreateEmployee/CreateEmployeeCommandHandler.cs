using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandHandler
{
    private readonly IEmployeeRepository _employeeRepository;

    public CreateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    public async Task<int> HandleAsync(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.PayrollNumber))
            throw new ValidationException(
                "El número de nómina es obligatorio.");

        if (string.IsNullOrWhiteSpace(command.FullName))
            throw new ValidationException(
                "El nombre del empleado es obligatorio.");

        if (command.PayrollNumber.Trim().Length > 30)
            throw new ValidationException(
                "El número de nómina no puede exceder 30 caracteres.");

        if (command.FullName.Trim().Length > 200)
            throw new ValidationException(
                "El nombre no puede exceder 200 caracteres.");

        var payrollNumber = command.PayrollNumber.Trim();

        var exists = await _employeeRepository
            .ExistsByPayrollNumberAsync(
                payrollNumber,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException(
                "Ya existe un empleado con ese número de nómina.");
        }

        var employee = new Employee
        {
            PayrollNumber = payrollNumber,
            FullName = command.FullName.Trim(),
            DepartmentId = command.DepartmentId,
            IsActive = true
        };

        await _employeeRepository.AddAsync(
            employee,
            cancellationToken);

        return employee.Id;
    }
}