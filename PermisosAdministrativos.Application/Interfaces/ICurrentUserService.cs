namespace PermisosAdministrativos.Application.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }

    Task<int?> GetEmployeeIdAsync(CancellationToken cancellationToken = default);
}
