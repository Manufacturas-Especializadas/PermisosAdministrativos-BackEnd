using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CompletePersonalPermit;

public class CompletePersonalPermitCommandHandler
{
    private readonly IPersonalPermitRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CompletePersonalPermitCommandHandler(
        IPersonalPermitRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
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
            throw new InvalidOperationException(
                "El permiso no está aprobado.");
        }

        permit.Status = PermitStatus.Completed;
        permit.ActualExitAt =
            _timeProvider.GetUtcNow().UtcDateTime;

        permit.ExitRegisteredByUserId =
            _currentUser.UserId;

        await _repository.UpdateAsync(
            permit,
            cancellationToken);
    }
}