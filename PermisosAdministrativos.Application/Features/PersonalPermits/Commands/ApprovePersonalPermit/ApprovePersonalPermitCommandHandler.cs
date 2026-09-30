using PermisosAdministrativos.Application.Interfaces;
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
            throw new InvalidOperationException(
                "El permiso ya fue procesado.");

        permit.Status = PermitStatus.Approved;
        permit.HumanResourcesReviewedAt =
            _timeProvider.GetUtcNow().UtcDateTime;
        permit.HumanResourcesReviewedByUserId =
            _currentUser.UserId;

        await _repository.UpdateAsync(
            permit,
            cancellationToken);
    }
}