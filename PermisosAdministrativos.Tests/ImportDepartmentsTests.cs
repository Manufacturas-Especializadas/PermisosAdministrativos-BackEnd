using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Departments.Commands.CreateDepartment;
using PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;
using PermisosAdministrativos.Infrastructure.Repositories;
using PermisosAdministrativos.Infrastructure.Services;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class ImportDepartmentsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ValidExcelCreatesActiveDepartmentsInOneBatch(int count)
    {
        using var context = new CountingContext();
        var repository = new RecordingRepository(context);
        using var stream = DepartmentSpreadsheetReaderTests.Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = "Departamento";
            for (var i = 0; i < count; i++)
                sheet.Cell(i + 2, 1).Value = new[] { "Calidad", "Producción", "Mantenimiento" }[i];
        });
        var result = await new ImportDepartmentsCommandHandler(
            new DepartmentSpreadsheetReader(), repository).HandleAsync(new(stream));

        Assert.Equal(count, result.Processed);
        Assert.Equal(count, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal(1, repository.BatchCalls);
        Assert.Equal(0, repository.SingleCalls);
        Assert.Equal(1, context.SaveCalls);
        context.ChangeTracker.Clear();
        var saved = await context.Departments.ToListAsync();
        Assert.Equal(count, saved.Count);
        Assert.All(saved, department => Assert.True(department.IsActive));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingDepartmentsAreIgnoredWithoutChanges(bool active)
    {
        using var context = new CountingContext();
        var existing = new Department { Name = "Calidad", IsActive = active };
        context.Departments.Add(existing);
        await context.SaveChangesAsync();
        context.SaveCalls = 0;
        var repository = new RecordingRepository(context);
        using var stream = DepartmentSpreadsheetReaderTests.Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = "Departamento";
            sheet.Cell(2, 1).Value = " Calidad ";
        });
        var result = await new ImportDepartmentsCommandHandler(
            new DepartmentSpreadsheetReader(), repository).HandleAsync(new(stream));
        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal(0, repository.BatchCalls);
        Assert.Equal(0, context.SaveCalls);
        context.ChangeTracker.Clear();
        var saved = Assert.Single(await context.Departments.ToListAsync());
        Assert.Equal(existing.Id, saved.Id);
        Assert.Equal("Calidad", saved.Name);
        Assert.Equal(active, saved.IsActive);
    }

    [Fact]
    public async Task MixedExcelContinuesAfterInvalidRowAndCountsDuplicates()
    {
        using var context = new CountingContext();
        var repository = new RecordingRepository(context);
        using var stream = DepartmentSpreadsheetReaderTests.Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = "Departamento";
            sheet.Cell(2, 1).Value = "Calidad";
            sheet.Cell(3, 1).Value = " calidad ";
            sheet.Cell(4, 2).Value = "Nombre obligatorio faltante";
            sheet.Cell(6, 1).Value = "Producción";
        });
        var result = await new ImportDepartmentsCommandHandler(
            new DepartmentSpreadsheetReader(), repository).HandleAsync(new(stream));
        Assert.Equal(4, result.Processed);
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Ignored);
        Assert.Equal("Fila 4: El nombre del departamento es obligatorio.", Assert.Single(result.Errors));
        Assert.Equal(1, repository.BatchCalls);
        Assert.Equal(1, context.SaveCalls);
        Assert.Equal(new[] { "Calidad", "Producción" },
            await context.Departments.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Calidad")]
    [InlineData("  Calidad  ")]
    [InlineData("100")]
    [InlineData("101")]
    public async Task NameValidationMatchesManualCreation(string input)
    {
        var name = input is "100" or "101" ? new string('A', int.Parse(input)) : input;
        using var manualContext = new CountingContext();
        using var importContext = new CountingContext();
        var manual = new CreateDepartmentCommandHandler(new DepartmentRepository(manualContext));
        var manualError = await Record.ExceptionAsync(() => manual.HandleAsync(new(name)));
        var repository = new RecordingRepository(importContext);
        var result = await new ImportDepartmentsCommandHandler(
            new RowsReader([new(9, name)]), repository).HandleAsync(new(Stream.Null));
        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Ignored);
        if (manualError is not null)
        {
            Assert.IsType<ValidationException>(manualError);
            Assert.Equal($"Fila 9: {manualError.Message}", Assert.Single(result.Errors));
            Assert.Equal(0, result.Created);
            Assert.Equal(0, repository.BatchCalls);
        }
        else
        {
            Assert.Empty(result.Errors);
            Assert.Equal(1, result.Created);
            var imported = Assert.Single(await importContext.Departments.ToListAsync());
            var created = Assert.Single(await manualContext.Departments.ToListAsync());
            Assert.Equal(created.Name, imported.Name);
            Assert.Equal(name.Trim(), imported.Name);
            Assert.Equal(created.IsActive, imported.IsActive);
        }
    }

    [Fact]
    public async Task TooLongNameDoesNotPreventFollowingValidDepartment()
    {
        using var context = new CountingContext();
        using var stream = DepartmentSpreadsheetReaderTests.Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = "Departamento";
            sheet.Cell(2, 1).Value = new string('A', 101);
            sheet.Cell(3, 1).Value = new string('A', 100);
        });
        var result = await new ImportDepartmentsCommandHandler(
            new DepartmentSpreadsheetReader(), new DepartmentRepository(context)).HandleAsync(new(stream));
        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Created);
        Assert.StartsWith("Fila 2:", Assert.Single(result.Errors));
        Assert.Equal(100, (await context.Departments.SingleAsync()).Name.Length);
    }

    [Fact]
    public async Task HeaderOnlyDoesNotSaveAnything()
    {
        using var context = new CountingContext();
        var repository = new RecordingRepository(context);
        using var stream = DepartmentSpreadsheetReaderTests.Excel(sheet => sheet.Cell(1, 1).Value = "Departamento");
        var result = await new ImportDepartmentsCommandHandler(
            new DepartmentSpreadsheetReader(), repository).HandleAsync(new(stream));
        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal(0, repository.BatchCalls);
        Assert.Equal(0, context.SaveCalls);
    }

    private sealed class RowsReader(List<DepartmentImportRow> rows) : IDepartmentSpreadsheetReader
    {
        public Task<List<DepartmentImportRow>> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
            => Task.FromResult(rows);
    }

    private sealed class CountingContext() : ApplicationDbContext(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
    {
        public int SaveCalls { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class RecordingRepository(ApplicationDbContext context) : IDepartmentRepository
    {
        private readonly DepartmentRepository _inner = new(context);
        public int BatchCalls { get; private set; }
        public int SingleCalls { get; private set; }

        public Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);
        public Task<List<Department>> GetAllAsync(CancellationToken cancellationToken = default)
            => _inner.GetAllAsync(cancellationToken);
        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
            => _inner.ExistsByNameAsync(name, cancellationToken);
        public Task AddAsync(Department department, CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return _inner.AddAsync(department, cancellationToken);
        }
        public Task AddRangeAsync(IEnumerable<Department> departments, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            return _inner.AddRangeAsync(departments, cancellationToken);
        }
    }
}
