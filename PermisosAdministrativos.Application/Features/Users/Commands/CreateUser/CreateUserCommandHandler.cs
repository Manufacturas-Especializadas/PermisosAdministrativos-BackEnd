using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandHandler
{
    private readonly IIdentityService _identityService;
    private readonly IEmployeeRepository _employeeRepository;

    public CreateUserCommandHandler(
        IIdentityService identityService,
        IEmployeeRepository employeeRepository)
    {
        _identityService = identityService;
        _employeeRepository = employeeRepository;
    }

    public async Task<string> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var validRoles = new[]
        {
            Roles.Administrator,
            Roles.Supervisor,
            Roles.HumanResources,
            Roles.Security
        };

        if (!validRoles.Contains(command.Role))
            throw new InvalidOperationException("Rol inválido.");

        if (command.EmployeeId.HasValue)
        {
            var employee = await _employeeRepository.GetByIdAsync(
                command.EmployeeId.Value,
                cancellationToken);

            if (employee is null || !employee.IsActive)
                throw new InvalidOperationException(
                    "El empleado no existe o está inactivo.");
        }

        return await _identityService.CreateUserAsync(
            command.UserName,
            command.Password,
            command.EmployeeId,
            command.Role);
    }
}