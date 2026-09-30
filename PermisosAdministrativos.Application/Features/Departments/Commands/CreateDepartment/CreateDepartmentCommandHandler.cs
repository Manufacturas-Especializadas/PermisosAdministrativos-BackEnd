using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Departments.Commands.CreateDepartment;

public class CreateDepartmentCommandHandler
{
    private readonly IDepartmentRepository _departmentRepository;

    public CreateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public async Task<int> HandleAsync(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var name = command.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException(
                "El nombre del departamento es obligatorio.");

        if (name.Length > 100)
            throw new ValidationException(
                "El nombre del departamento no puede exceder 100 caracteres.");

        if (await _departmentRepository.ExistsByNameAsync(
                name,
                cancellationToken))
        {
            throw new ConflictException(
                "Ya existe un departamento con ese nombre.");
        }

        var department = new Department
        {
            Name = name,
            IsActive = true
        };

        await _departmentRepository.AddAsync(
            department,
            cancellationToken);

        return department.Id;
    }
}