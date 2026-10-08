using PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;

namespace PermisosAdministrativos.Application.Interfaces;

public interface IDepartmentSpreadsheetReader
{
    Task<List<DepartmentImportRow>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
