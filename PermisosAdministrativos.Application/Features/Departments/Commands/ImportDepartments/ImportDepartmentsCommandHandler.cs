using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;

public class ImportDepartmentsCommandHandler
{
    private readonly IDepartmentSpreadsheetReader _spreadsheetReader;
    private readonly IDepartmentRepository _departmentRepository;

    public ImportDepartmentsCommandHandler(
        IDepartmentSpreadsheetReader spreadsheetReader,
        IDepartmentRepository departmentRepository)
    {
        _spreadsheetReader = spreadsheetReader;
        _departmentRepository = departmentRepository;
    }

    public async Task<DepartmentImportResult> HandleAsync(
        ImportDepartmentsCommand command,
        CancellationToken cancellationToken = default)
    {
        var rows = await _spreadsheetReader.ReadAsync(
            command.FileStream, cancellationToken);

        var result = new DepartmentImportResult { Processed = rows.Count };
        var namesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var departmentsToCreate = new List<Department>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = row.Name.Trim();

            // Keep these rules aligned with CreateDepartmentCommandHandler.
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: El nombre del departamento es obligatorio.");
                continue;
            }

            if (name.Length > 100)
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: El nombre del departamento no puede exceder 100 caracteres.");
                continue;
            }

            if (!namesInFile.Add(name) ||
                await _departmentRepository.ExistsByNameAsync(name, cancellationToken))
            {
                result.Ignored++;
                continue;
            }

            departmentsToCreate.Add(new Department
            {
                Name = name,
                IsActive = true
            });
        }

        if (departmentsToCreate.Count > 0)
        {
            await _departmentRepository.AddRangeAsync(
                departmentsToCreate, cancellationToken);
        }

        result.Created = departmentsToCreate.Count;
        return result;
    }
}
