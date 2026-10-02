using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Domain.Interfaces;

public interface IPersonalPermitRepository
{
    Task AddAsync(
        PersonalPermit permit,
        CancellationToken cancellationToken = default);

    Task<PersonalPermit?> GetByIdAsync(
    int id,
    CancellationToken cancellationToken = default);

    Task<List<PersonalPermit>> GetPendingAsync(
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PersonalPermit permit,
        CancellationToken cancellationToken = default);

    Task<bool> TryApproveAsync(
        int id,
        string userId,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default);

    Task<bool> TryRejectAsync(
        int id,
        string userId,
        string reason,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default);

    Task<bool> TryCompleteAsync(
        int id,
        string userId,
        DateTime actualExitAt,
        CancellationToken cancellationToken = default);

    Task<List<PersonalPermit>> GetApprovedAsync(
    DateOnly permitDate,
    CancellationToken cancellationToken = default);

    Task<List<PersonalPermit>> GetPagedAsync(
    int? employeeId,
    PermitStatus? status,
    DateOnly? fromDate,
    DateOnly? toDate,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        int? employeeId,
        PermitStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);

}