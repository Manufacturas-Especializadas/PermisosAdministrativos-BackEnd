using System.Xml;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;
using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Infrastructure.Services;

public class DepartmentSpreadsheetReader : IDepartmentSpreadsheetReader
{
    public Task<List<DepartmentImportRow>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = OpenWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        var header = worksheet?.Row(1).CellsUsed().FirstOrDefault(cell =>
            cell.GetString().Trim().Equals("Departamento", StringComparison.OrdinalIgnoreCase));

        if (worksheet is null || header is null)
            throw new ValidationException("Faltan las columnas obligatorias: Departamento.");

        var departmentColumn = header.Address.ColumnNumber;
        var rows = new List<DepartmentImportRow>();

        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row.CellsUsed().All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
                continue;

            rows.Add(new DepartmentImportRow(
                row.RowNumber(), row.Cell(departmentColumn).GetString().Trim()));
        }

        return Task.FromResult(rows);
    }

    private static XLWorkbook OpenWorkbook(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        try
        {
            // ClosedXML assumes the package has a workbook part; check it before loading.
            using (var document = SpreadsheetDocument.Open(buffer, false))
            {
                if (document.WorkbookPart?.Workbook is null)
                    throw new ValidationException("El archivo no es un Excel .xlsx válido.");
            }

            buffer.Position = 0;
            return new XLWorkbook(buffer);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or FileFormatException or OpenXmlPackageException or XmlException
            or FormatException or ArgumentException)
        {
            throw new ValidationException("El archivo no es un Excel .xlsx válido.");
        }
    }
}
