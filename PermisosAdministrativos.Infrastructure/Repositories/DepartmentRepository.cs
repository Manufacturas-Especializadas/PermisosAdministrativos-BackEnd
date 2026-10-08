using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;

namespace PermisosAdministrativos.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly ApplicationDbContext _context;

    public DepartmentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Department?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<List<Department>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.Departments
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return _context.Departments
            .AnyAsync(
                x => x.Name == name,
                cancellationToken);
    }

    public async Task AddAsync(
        Department department,
        CancellationToken cancellationToken = default)
    {
        await _context.Departments.AddAsync(
            department,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<Department> departments,
        CancellationToken cancellationToken = default)
    {
        await _context.Departments.AddRangeAsync(departments, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
