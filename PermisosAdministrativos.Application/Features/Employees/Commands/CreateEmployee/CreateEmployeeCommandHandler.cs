using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Application.Features.Employees.Common;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandHandler
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;

    public CreateEmployeeCommandHandler(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
    }

    public async Task<int> HandleAsync(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        var (payrollNumber, fullName) = EmployeeValidation.ValidateFields(
            command.PayrollNumber, command.FullName);

        var exists = await _employeeRepository
            .ExistsByPayrollNumberAsync(
                payrollNumber,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException(
                "Ya existe un empleado con ese número de nómina.");
        }

        var department = await _departmentRepository.GetByIdAsync(
            command.DepartmentId,
            cancellationToken);

        EmployeeValidation.ValidateDepartment(department);

        var employee = new Employee
        {
            PayrollNumber = payrollNumber,
            FullName = fullName,
            DepartmentId = command.DepartmentId,
            IsActive = true
        };

        await _employeeRepository.AddAsync(
            employee,
            cancellationToken);

        return employee.Id;
    }
}
