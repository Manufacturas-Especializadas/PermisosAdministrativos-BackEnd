using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Employees.Common;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Commands.UpdateEmployee;

public class UpdateEmployeeCommandHandler
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;

    public UpdateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
    }

    public async Task HandleAsync(
        UpdateEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(
            command.EmployeeId, cancellationToken)
            ?? throw new NotFoundException("El empleado no existe.");

        var (payrollNumber, fullName) = EmployeeValidation.ValidateFields(
            command.PayrollNumber, command.FullName);

        if (await _employeeRepository.ExistsByPayrollNumberExcludingEmployeeAsync(
                payrollNumber, command.EmployeeId, cancellationToken))
        {
            throw new ConflictException("Ya existe un empleado con ese número de nómina.");
        }

        var department = await _departmentRepository.GetByIdAsync(
            command.DepartmentId, cancellationToken);
        EmployeeValidation.ValidateDepartment(department);

        employee.PayrollNumber = payrollNumber;
        employee.FullName = fullName;
        employee.DepartmentId = command.DepartmentId;

        await _employeeRepository.UpdateAsync(employee, cancellationToken);
    }
}
