using System.IO.Compression;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Infrastructure.Services;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class EmployeeSpreadsheetReaderTests
{
    internal static MemoryStream Excel(Action<IXLWorksheet>? configure = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Empleados");
        sheet.Cell(1, 1).Value = "NumeroNomina";
        sheet.Cell(1, 2).Value = "NombreCompleto";
        sheet.Cell(1, 3).Value = "Departamento";
        configure?.Invoke(sheet);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task HeadersIgnoreCaseAndSpacesAndCanBeReordered()
    {
        using var stream = Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = " departamento ";
            sheet.Cell(1, 2).Value = " NUMERONOMINA ";
            sheet.Cell(1, 3).Value = " nombrecompleto ";
            sheet.Cell(2, 1).Value = " Calidad ";
            sheet.Cell(2, 2).Value = " 001 ";
            sheet.Cell(2, 3).Value = " Ana ";
        });
        var row = Assert.Single(await new EmployeeSpreadsheetReader().ReadAsync(stream));
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("001", row.PayrollNumber);
        Assert.Equal("Ana", row.FullName);
        Assert.Equal("Calidad", row.DepartmentName);
    }

    [Fact]
    public async Task BlankRowsAreSkippedButPartialRowsRetainRealRowNumbers()
    {
        using var stream = Excel(sheet =>
        {
            sheet.Cell(3, 1).Value = "   ";
            sheet.Cell(5, 2).Value = "Ana";
            sheet.Cell(8, 1).Value = "002";
            sheet.Cell(8, 2).Value = "Bruno";
            sheet.Cell(8, 3).Value = "Calidad";
        });
        var rows = await new EmployeeSpreadsheetReader().ReadAsync(stream);
        Assert.Equal(new[] { 5, 8 }, rows.Select(x => x.RowNumber));
        Assert.Equal("", rows[0].PayrollNumber);
        Assert.Equal("", rows[0].DepartmentName);
    }

    [Fact]
    public async Task ReadsOnlyFirstSheet()
    {
        using var initial = Excel();
        using var workbook = new XLWorkbook(initial);
        var second = workbook.AddWorksheet("Segunda");
        second.Cell(1, 1).Value = "NumeroNomina";
        second.Cell(1, 2).Value = "NombreCompleto";
        second.Cell(1, 3).Value = "Departamento";
        second.Cell(2, 1).Value = "001";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        Assert.Empty(await new EmployeeSpreadsheetReader().ReadAsync(stream));
    }

    [Fact]
    public async Task HeaderOnlyReturnsNoRows()
    {
        using var stream = Excel();
        Assert.Empty(await new EmployeeSpreadsheetReader().ReadAsync(stream));
    }

    [Theory]
    [InlineData(1, "NumeroNomina")]
    [InlineData(2, "NombreCompleto")]
    [InlineData(3, "Departamento")]
    public async Task MissingRequiredHeaderIsValidationError(int column, string header)
    {
        using var stream = Excel(sheet => sheet.Cell(1, column).Value = "Otro");
        var error = await Assert.ThrowsAsync<ValidationException>(() => new EmployeeSpreadsheetReader().ReadAsync(stream));
        Assert.Contains(header, error.Message);
    }

    [Fact]
    public async Task HeadersMustBeInFirstRow()
    {
        using var stream = Excel(sheet => sheet.Row(1).InsertRowsAbove(1));
        await Assert.ThrowsAsync<ValidationException>(() => new EmployeeSpreadsheetReader().ReadAsync(stream));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EmptyCorruptAndNonWorkbookZipAreValidationErrors(int kind)
    {
        using var stream = new MemoryStream();
        if (kind == 1)
            stream.Write("No es un Excel"u8);
        if (kind == 2)
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, true);
            using var writer = new StreamWriter(zip.CreateEntry("otro.txt").Open());
            writer.Write("No es un libro Excel");
        }
        stream.Position = 0;
        var error = await Assert.ThrowsAsync<ValidationException>(() => new EmployeeSpreadsheetReader().ReadAsync(stream));
        Assert.Equal("El archivo no es un Excel .xlsx válido.", error.Message);
    }

    [Fact]
    public async Task WorkbookWithoutSheetsIsValidationError()
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var part = document.AddWorkbookPart();
            part.Workbook = new Workbook(new Sheets());
            part.Workbook.Save();
        }
        stream.Position = 0;
        var error = await Assert.ThrowsAsync<ValidationException>(() => new EmployeeSpreadsheetReader().ReadAsync(stream));
        Assert.Equal("El archivo no contiene una hoja de empleados.", error.Message);
    }
}
