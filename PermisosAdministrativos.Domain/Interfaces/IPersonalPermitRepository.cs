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

    Task<List<PersonalPermit>> GetApprovedAsync(
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