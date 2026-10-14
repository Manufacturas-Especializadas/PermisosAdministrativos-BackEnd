using PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;

namespace PermisosAdministrativos.Application.Interfaces;

public interface IUserImportService
{
    Task<UserImportResult> ImportAsync(
        IReadOnlyList<UserImportRow> rows,
        CancellationToken cancellationToken = default);
}