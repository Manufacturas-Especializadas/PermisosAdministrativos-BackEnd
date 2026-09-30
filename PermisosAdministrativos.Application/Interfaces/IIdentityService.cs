namespace PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Application.DTOs;

public interface IIdentityService
{
    Task<string> CreateUserAsync(
        string userName,
        string password,
        int? employeeId,
        string role);

    Task<List<UserDto>> GetUsersAsync(
    CancellationToken cancellationToken = default);

}