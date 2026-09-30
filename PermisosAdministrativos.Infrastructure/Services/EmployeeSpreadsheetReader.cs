using ClosedXML.Excel;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;
using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Infrastructure.Services;

public class EmployeeSpreadsheetReader : IEmployeeSpreadsheetReader
{
    public Task<List<EmployeeImportRow>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.First();

        var headerColumns = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim();

            if (!string.IsNullOrWhiteSpace(header))
            {
                headerColumns.TryAdd(
                    header,
                    cell.Address.ColumnNumber);
            }
        }

        var requiredHeaders = new[]
        {
            "NumeroNomina",
            "NombreCompleto",
            "Departamento"
        };

        var missingHeaders = requiredHeaders
            .Where(x => !headerColumns.ContainsKey(x))
            .ToList();

        if (missingHeaders.Count > 0)
        {
            throw new ValidationException(
                $"Faltan las columnas obligatorias: {string.Join(", ", missingHeaders)}.");
        }

        var payrollColumn = headerColumns["NumeroNomina"];
        var nameColumn = headerColumns["NombreCompleto"];
        var departmentColumn = headerColumns["Departamento"];

        var rows = new List<EmployeeImportRow>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payrollNumber =
                row.Cell(payrollColumn).GetString().Trim();

            var fullName =
                row.Cell(nameColumn).GetString().Trim();

            var department =
                row.Cell(departmentColumn).GetString().Trim();

            if (string.IsNullOrWhiteSpace(payrollNumber) &&
                string.IsNullOrWhiteSpace(fullName) &&
                string.IsNullOrWhiteSpace(department))
            {
                continue;
            }

            rows.Add(new EmployeeImportRow(
                row.RowNumber(),
                payrollNumber,
                fullName,
                department));
        }

        return Task.FromResult(rows);
    }
}