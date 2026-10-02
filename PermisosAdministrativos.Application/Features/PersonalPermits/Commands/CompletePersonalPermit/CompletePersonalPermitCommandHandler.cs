using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CompletePersonalPermit;

public class CompletePersonalPermitCommandHandler
{
    private readonly IPersonalPermitRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IBusinessDateService _businessDateService;

    public CompletePersonalPermitCommandHandler(
        IPersonalPermitRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        IBusinessDateService businessDateService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _businessDateService = businessDateService;
    }

    public async Task HandleAsync(
        CompletePersonalPermitCommand command,
        CancellationToken cancellationToken = default)
    {
        var permit = await _repository.GetByIdAsync(
            command.PermitId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "El permiso no existe.");

        if (permit.Status != PermitStatus.Approved)
        {
            throw new ConflictException(
                "El permiso ya no está disponible para registrar la salida.");
        }

        if (permit.PermitDate != _businessDateService.Today)
        {
            throw new ValidationException(
                "Solo se pueden registrar salidas correspondientes al día actual.");
        }

        var completed = await _repository.TryCompleteAsync(
            command.PermitId,
            _currentUser.UserId,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (!completed)
            throw new ConflictException(
                "El permiso ya no está disponible para registrar la salida.");
    }
}