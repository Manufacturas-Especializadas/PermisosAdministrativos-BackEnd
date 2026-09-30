using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;

namespace PermisosAdministrativos.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly ApplicationDbContext _context;

    public EmployeeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Employee?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return _context.Employees
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<List<Employee>> GetAllAsync(
    CancellationToken cancellationToken = default)
    {
        return _context.Employees
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByPayrollNumberAsync(
    string payrollNumber,
    CancellationToken cancellationToken = default)
    {
        return _context.Employees
            .AnyAsync(
                x => x.PayrollNumber == payrollNumber,
                cancellationToken);
    }

    public async Task AddAsync(
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        await _context.Employees.AddAsync(
            employee,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<string>> GetExistingPayrollNumbersAsync(
    IEnumerable<string> payrollNumbers,
    CancellationToken cancellationToken = default)
    {
        return _context.Employees
            .AsNoTracking()
            .Where(x => payrollNumbers.Contains(x.PayrollNumber))
            .Select(x => x.PayrollNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IEnumerable<Employee> employees,
        CancellationToken cancellationToken = default)
    {
        await _context.Employees.AddRangeAsync(
            employees,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
    Employee employee,
    CancellationToken cancellationToken = default)
    {
        _context.Employees.Update(employee);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Employee>> GetPagedAsync(
    string? search,
    bool? isActive,
    int? departmentId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search, isActive, departmentId)
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        string? search,
        bool? isActive,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        return BuildQuery(search, isActive, departmentId)
            .CountAsync(cancellationToken);
    }

    private IQueryable<Employee> BuildQuery(
    string? search,
    bool? isActive,
    int? departmentId)
    {
        var query = _context.Employees
            .Include(x => x.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(x =>
                x.FullName.Contains(search) ||
                x.PayrollNumber.Contains(search));
        }

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (departmentId.HasValue)
            query = query.Where(x => x.DepartmentId == departmentId.Value);

        return query;
    }

}