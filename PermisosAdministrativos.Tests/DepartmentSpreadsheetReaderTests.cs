using System.IO.Compression;
using ClosedXML.Excel;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Infrastructure.Services;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class DepartmentSpreadsheetReaderTests
{
    internal static MemoryStream Excel(Action<IXLWorksheet> configure)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Departamentos");
        configure(sheet);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task HeaderIsCaseInsensitiveTrimmedAndCanBeInAnyColumn()
    {
        using var stream = Excel(sheet =>
        {
            sheet.Cell(1, 3).Value = "  dEpArTaMeNtO  ";
            sheet.Cell(2, 3).Value = "  Calidad  ";
        });
        var rows = await new DepartmentSpreadsheetReader().ReadAsync(stream);
        var row = Assert.Single(rows);
        Assert.Equal("Calidad", row.Name);
        Assert.Equal(2, row.RowNumber);
    }

    [Fact]
    public async Task OnlyFirstSheetIsRead()
    {
        using var workbook = new XLWorkbook();
        var first = workbook.AddWorksheet("Primera");
        first.Cell(1, 1).Value = "Departamento";
        first.Cell(2, 1).Value = "Calidad";
        var second = workbook.AddWorksheet("Segunda");
        second.Cell(1, 1).Value = "Departamento";
        second.Cell(2, 1).Value = "Producción";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var row = Assert.Single(await new DepartmentSpreadsheetReader().ReadAsync(stream));
        Assert.Equal("Calidad", row.Name);
    }

    [Fact]
    public async Task BlankRowsAreSkippedAndRealRowNumbersArePreserved()
    {
        using var stream = Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = "Departamento";
            sheet.Cell(3, 1).Value = "   ";
            sheet.Cell(5, 2).Value = "Fila con nombre faltante";
            sheet.Cell(8, 1).Value = "Calidad";
        });
        var rows = await new DepartmentSpreadsheetReader().ReadAsync(stream);
        Assert.Equal(new[] { 5, 8 }, rows.Select(row => row.RowNumber));
        Assert.Equal(string.Empty, rows[0].Name);
    }

    [Fact]
    public async Task HeaderWithoutDataReturnsNoRows()
    {
        using var stream = Excel(sheet => sheet.Cell(1, 1).Value = "Departamento");
        Assert.Empty(await new DepartmentSpreadsheetReader().ReadAsync(stream));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Nombre")]
    public async Task MissingHeaderIsAValidationError(string header)
    {
        using var stream = Excel(sheet => sheet.Cell(1, 1).Value = header);
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            new DepartmentSpreadsheetReader().ReadAsync(stream));
        Assert.Equal("Faltan las columnas obligatorias: Departamento.", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeaderMustBeOnFirstRowOfFirstSheet(bool secondSheet)
    {
        using var workbook = new XLWorkbook();
        var first = workbook.AddWorksheet("Primera");
        first.Cell(1, 1).Value = "Otro";
        if (secondSheet)
            workbook.AddWorksheet("Segunda").Cell(1, 1).Value = "Departamento";
        else
            first.Cell(2, 1).Value = "Departamento";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        await Assert.ThrowsAsync<ValidationException>(() =>
            new DepartmentSpreadsheetReader().ReadAsync(stream));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CorruptFilesBecomeValidationErrors(int kind)
    {
        using var stream = new MemoryStream();
        if (kind == 1)
            stream.Write("Esto no es un Excel"u8);
        if (kind == 2)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true);
            using var writer = new StreamWriter(archive.CreateEntry("otro.txt").Open());
            writer.Write("No contiene un libro Excel");
        }
        stream.Position = 0;
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            new DepartmentSpreadsheetReader().ReadAsync(stream));
        Assert.Equal("El archivo no es un Excel .xlsx válido.", error.Message);
    }
}
