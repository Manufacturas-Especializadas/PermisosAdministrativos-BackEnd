using PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;

namespace PermisosAdministrativos.Application.Interfaces;

public interface IEmployeeSpreadsheetReader
{
    Task<List<EmployeeImportRow>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}