using PermisosAdministrativos.Domain.Entities;

namespace PermisosAdministrativos.Domain.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<List<Employee>> GetPagedAsync(
    string? search,
    bool? isActive,
    int? departmentId,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string? search,
        bool? isActive,
        int? departmentId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByPayrollNumberAsync(
    string payrollNumber,
    CancellationToken cancellationToken = default);

    Task AddAsync(
        Employee employee,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetExistingPayrollNumbersAsync(
    IEnumerable<string> payrollNumbers,
    CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<Employee> employees,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
    Employee employee,
    CancellationToken cancellationToken = default);

}