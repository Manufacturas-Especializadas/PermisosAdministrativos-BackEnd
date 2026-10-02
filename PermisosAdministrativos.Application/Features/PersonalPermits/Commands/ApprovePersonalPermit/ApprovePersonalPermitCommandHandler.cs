using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.PersonalPermits.Commands.ApprovePersonalPermit;

public class ApprovePersonalPermitCommandHandler
{
    private readonly IPersonalPermitRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ApprovePersonalPermitCommandHandler(
        IPersonalPermitRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(
        ApprovePersonalPermitCommand command,
        CancellationToken cancellationToken = default)
    {
        var permit = await _repository.GetByIdAsync(
            command.PermitId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "El permiso no existe.");

        if (permit.Status != PermitStatus.PendingHumanResourcesApproval)
            throw new ConflictException(
                "El permiso ya fue procesado.");

        var approved = await _repository.TryApproveAsync(
            command.PermitId,
            _currentUser.UserId,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (!approved)
            throw new ConflictException("El permiso ya fue procesado.");
    }
}