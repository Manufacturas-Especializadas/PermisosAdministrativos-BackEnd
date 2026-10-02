using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;

namespace PermisosAdministrativos.Infrastructure.Repositories;

public class PersonalPermitRepository : IPersonalPermitRepository
{
    private readonly ApplicationDbContext _context;

    public PersonalPermitRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        PersonalPermit permit,
        CancellationToken cancellationToken = default)
    {
        await _context.PersonalPermits.AddAsync(
            permit,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<PersonalPermit?> GetByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        return _context.PersonalPermits
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<List<PersonalPermit>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.PersonalPermits
            .AsNoTracking()
            .Include(x => x.Employee)
            .Where(x =>
                x.Status == PermitStatus.PendingHumanResourcesApproval)
            .OrderBy(x => x.PermitDate)
            .ThenBy(x => x.ExitTime)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PersonalPermit permit,
        CancellationToken cancellationToken = default)
    {
        _context.PersonalPermits.Update(permit);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryApproveAsync(
        int id,
        string userId,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.PersonalPermits
            .Where(x => x.Id == id
                && x.Status == PermitStatus.PendingHumanResourcesApproval)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, PermitStatus.Approved)
                .SetProperty(x => x.HumanResourcesReviewedAt, reviewedAt)
                .SetProperty(x => x.HumanResourcesReviewedByUserId, userId),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryRejectAsync(
        int id,
        string userId,
        string reason,
        DateTime reviewedAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.PersonalPermits
            .Where(x => x.Id == id
                && x.Status == PermitStatus.PendingHumanResourcesApproval)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, PermitStatus.Rejected)
                .SetProperty(x => x.RejectionReason, reason)
                .SetProperty(x => x.HumanResourcesReviewedAt, reviewedAt)
                .SetProperty(x => x.HumanResourcesReviewedByUserId, userId),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryCompleteAsync(
        int id,
        string userId,
        DateTime actualExitAt,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.PersonalPermits
            .Where(x => x.Id == id && x.Status == PermitStatus.Approved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, PermitStatus.Completed)
                .SetProperty(x => x.ActualExitAt, actualExitAt)
                .SetProperty(x => x.ExitRegisteredByUserId, userId),
                cancellationToken);

        return affectedRows == 1;
    }

    public Task<List<PersonalPermit>> GetApprovedAsync(
    DateOnly permitDate,
    CancellationToken cancellationToken = default)
    {
        return _context.PersonalPermits
            .AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => x.Status == PermitStatus.Approved
                && x.PermitDate == permitDate)
            .OrderBy(x => x.PermitDate)
            .ThenBy(x => x.ExitTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonalPermit>> GetAllAsync(
    int? employeeId,
    PermitStatus? status,
    DateOnly? fromDate,
    DateOnly? toDate,
    CancellationToken cancellationToken = default)
    {
        var query = _context.PersonalPermits
            .AsNoTracking()
            .Include(x => x.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(x => x.EmployeeId == employeeId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.PermitDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.PermitDate <= toDate.Value);

        return await query
            .OrderByDescending(x => x.PermitDate)
            .ThenByDescending(x => x.ExitTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonalPermit>> GetPagedAsync(
    int? employeeId,
    PermitStatus? status,
    DateOnly? fromDate,
    DateOnly? toDate,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
    {
        return await BuildQuery(
                employeeId,
                status,
                fromDate,
                toDate)
            .AsNoTracking()
            .Include(x => x.Employee)
            .OrderByDescending(x => x.PermitDate)
            .ThenByDescending(x => x.ExitTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        int? employeeId,
        PermitStatus? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        return BuildQuery(
                employeeId,
                status,
                fromDate,
                toDate)
            .CountAsync(cancellationToken);
    }



    private IQueryable<PersonalPermit> BuildQuery(
    int? employeeId,
    PermitStatus? status,
    DateOnly? fromDate,
    DateOnly? toDate)
    {
        var query = _context.PersonalPermits.AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(x => x.EmployeeId == employeeId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(x => x.PermitDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.PermitDate <= toDate.Value);

        return query;
    }

}