using PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;

namespace PermisosAdministrativos.Application.Interfaces;

public interface IUserSpreadsheetReader
{
    Task<List<UserImportRow>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}