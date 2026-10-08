using PermisosAdministrativos.Domain.Entities;

namespace PermisosAdministrativos.Domain.Interfaces;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<List<Department>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Department department,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<Department> departments,
        CancellationToken cancellationToken = default);
}
