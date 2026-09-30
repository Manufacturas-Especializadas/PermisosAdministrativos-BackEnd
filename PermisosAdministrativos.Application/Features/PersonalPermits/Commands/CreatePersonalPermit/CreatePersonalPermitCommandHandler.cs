using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CreatePersonalPermit;

public class CreatePersonalPermitCommandHandler
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPersonalPermitRepository _permitRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public CreatePersonalPermitCommandHandler(
        IEmployeeRepository employeeRepository,
        IPersonalPermitRepository permitRepository,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _employeeRepository = employeeRepository;
        _permitRepository = permitRepository;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task<int> HandleAsync(
        CreatePersonalPermitCommand command,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(
            command.EmployeeId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ValidationException(
                "El motivo es obligatorio.");

        if (command.Reason.Trim().Length > 500)
            throw new ValidationException(
                "El motivo no puede exceder 500 caracteres.");

        if (!Enum.IsDefined(command.PermitType))
            throw new ValidationException(
                "El tipo de permiso no es válido.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ValidationException(
                "El motivo es obligatorio.");

        if (command.Reason.Trim().Length > 500)
            throw new ValidationException(
                "El motivo no puede exceder 500 caracteres.");

        if (!Enum.IsDefined(command.PermitType))
            throw new ValidationException(
                "El tipo de permiso no es válido.");


        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userId = _currentUserService.UserId;

        var permit = new PersonalPermit
        {
            EmployeeId = command.EmployeeId,
            PermitDate = command.PermitDate,
            ExitTime = command.ExitTime,
            PermitType = command.PermitType,
            Reason = command.Reason,

            Status = PermitStatus.PendingHumanResourcesApproval,

            CreatedAt = now,
            CreatedByUserId = userId,

            SupervisorApprovedAt = now,
            SupervisorApprovedByUserId = userId
        };

        await _permitRepository.AddAsync(permit, cancellationToken);

        return permit.Id;
    }
}