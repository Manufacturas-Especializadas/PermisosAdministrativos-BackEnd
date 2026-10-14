using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Repositories;
using PermisosAdministrativos.Infrastructure.Services;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class ImportEmployeesTests
{
    internal static MemoryStream ExcelRows(params (string Payroll, string Name, string Department)[] rows)
        => EmployeeSpreadsheetReaderTests.Excel(sheet =>
        {
            for (var i = 0; i < rows.Length; i++)
            {
                sheet.Cell(i + 2, 1).Value = rows[i].Payroll;
                sheet.Cell(i + 2, 2).Value = rows[i].Name;
                sheet.Cell(i + 2, 3).Value = rows[i].Department;
            }
        });

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ValidRowsAreCreatedActiveInOneBatchAndOneSave(int count)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        var repository = new RecordingEmployeeRepository(context);
        using var stream = ExcelRows(Enumerable.Range(1, count)
            .Select(i => ($" 00{i} ", $" Empleado {i} ", " calidad ")).ToArray());
        var result = await Handler(context, repository).HandleAsync(new(stream));
        Assert.Equal(count, result.Processed);
        Assert.Equal(count, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal(1, repository.BatchCalls);
        Assert.Equal(0, repository.SingleCalls);
        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(1, context.SaveCalls);
        context.ChangeTracker.Clear();
        var employees = await context.Employees.OrderBy(x => x.PayrollNumber).ToListAsync();
        Assert.Equal(count, employees.Count);
        Assert.All(employees, employee =>
        {
            Assert.Equal(employee.PayrollNumber.Trim(), employee.PayrollNumber);
            Assert.Equal(employee.FullName.Trim(), employee.FullName);
            Assert.True(employee.IsActive);
            Assert.Equal(1, employee.DepartmentId);
        });
    }

    [Fact]
    public async Task MaximumLengthsAreAcceptedAfterTrimming()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        using var stream = ExcelRows(($" {new string('N', 30)} ", $" {new string('A', 200)} ", "Calidad"));
        var result = await Handler(context).HandleAsync(new(stream));
        Assert.Equal(1, result.Created);
        Assert.Empty(result.Errors);
        var saved = await context.Employees.SingleAsync();
        Assert.Equal(30, saved.PayrollNumber.Length);
        Assert.Equal(200, saved.FullName.Length);
    }

    [Theory]
    [MemberData(nameof(CreateEmployeeTests.InvalidFields), MemberType = typeof(CreateEmployeeTests))]
    public async Task RejectsSameInvalidFieldsAsCreateAndContinues(string? payroll, string? name, string message)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        using var stream = ExcelRows((payroll ?? "", name ?? "", "Calidad"), ("003", "Válido", "Calidad"));
        var result = await Handler(context).HandleAsync(new(stream));
        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Ignored);
        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Fila 2:", error);
        Assert.EndsWith(string.IsNullOrWhiteSpace(payroll) || string.IsNullOrWhiteSpace(name)
            ? "faltan datos obligatorios." : message, error);
        Assert.Equal("003", (await context.Employees.SingleAsync()).PayrollNumber);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingEmployeeIsIgnoredWithoutUpdatingOrReactivating(bool active)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(employeeActive: active);
        var repository = new RecordingEmployeeRepository(context);
        using var stream = ExcelRows((" 001 ", "Otro nombre", "Producción"));
        var result = await Handler(context, repository).HandleAsync(new(stream));
        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal(0, repository.BatchCalls);
        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal(0, context.SaveCalls);
        var saved = await context.Employees.SingleAsync();
        Assert.Equal("Ana", saved.FullName);
        Assert.Equal(1, saved.DepartmentId);
        Assert.Equal(active, saved.IsActive);
    }

    [Fact]
    public async Task RepeatedPayrollInFileIsIgnoredAfterFirstValidRow()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        using var stream = ExcelRows(("abc", "Ana", "Calidad"), (" ABC ", "Otro", "Producción"));
        var result = await Handler(context).HandleAsync(new(stream));
        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal("Ana", (await context.Employees.SingleAsync()).FullName);
    }

    [Theory]
    [InlineData("No existe", "no existe")]
    [InlineData("Inactivo", "inactivo")]
    [InlineData("", "faltan datos obligatorios")]
    public async Task InvalidDepartmentDoesNotReservePayroll(string department, string reason)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        using var stream = ExcelRows(("001", "Inválido", department), ("001", "Válido", "Calidad"));
        var result = await Handler(context).HandleAsync(new(stream));
        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Contains(reason, Assert.Single(result.Errors));
        Assert.Equal("Válido", (await context.Employees.SingleAsync()).FullName);
    }

    [Fact]
    public async Task TooLongNameDoesNotReservePayroll()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        using var stream = ExcelRows(("001", new string('A', 201), "Calidad"), ("001", "Válido", "Calidad"));
        var result = await Handler(context).HandleAsync(new(stream));
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Single(result.Errors);
        Assert.Equal("Válido", (await context.Employees.SingleAsync()).FullName);
    }

    [Fact]
    public async Task MixedResultCountsAllRowsAndReportsRealExcelRow()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        var repository = new RecordingEmployeeRepository(context);
        using var stream = ExcelRows(
            ("001", "Existente", "Calidad"),
            ("002", "Nuevo", "Calidad"),
            ("003", new string('A', 201), "Calidad"),
            ("003", "Válido", "Producción"));
        var result = await Handler(context, repository).HandleAsync(new(stream));
        Assert.Equal(4, result.Processed);
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Ignored);
        Assert.Equal("Fila 4: El nombre no puede exceder 200 caracteres.", Assert.Single(result.Errors));
        Assert.Equal(1, repository.BatchCalls);
        Assert.Equal(1, context.SaveCalls);
        Assert.Equal(3, await context.Employees.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoAcceptedRowsDoesNotSave(bool invalidRow)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        var repository = new RecordingEmployeeRepository(context);
        using var stream = invalidRow ? ExcelRows(("001", "", "Calidad")) : ExcelRows();
        var result = await Handler(context, repository).HandleAsync(new(stream));
        Assert.Equal(invalidRow ? 1 : 0, result.Processed);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Equal(invalidRow ? 1 : 0, result.Errors.Count);
        Assert.Equal(0, repository.BatchCalls);
        Assert.Equal(0, context.SaveCalls);
    }

    private static ImportEmployeesCommandHandler Handler(EmployeeTestContext context, IEmployeeRepository? repository = null)
        => new(new EmployeeSpreadsheetReader(), repository ?? new EmployeeRepository(context), new DepartmentRepository(context));

    private sealed class RecordingEmployeeRepository(EmployeeTestContext context) : IEmployeeRepository
    {
        private readonly EmployeeRepository _inner = new(context);
        public int BatchCalls { get; private set; }
        public int SingleCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);
        public Task<Employee?> GetByIdWithDepartmentAsync(int id, CancellationToken cancellationToken = default)
            => _inner.GetByIdWithDepartmentAsync(id, cancellationToken);
        public Task<bool> ExistsByPayrollNumberExcludingEmployeeAsync(string payrollNumber, int employeeId, CancellationToken cancellationToken = default)
            => _inner.ExistsByPayrollNumberExcludingEmployeeAsync(payrollNumber, employeeId, cancellationToken);
        public Task<List<Employee>> GetPagedAsync(string? search, bool? isActive, int? departmentId, int page, int pageSize, CancellationToken cancellationToken = default)
            => _inner.GetPagedAsync(search, isActive, departmentId, page, pageSize, cancellationToken);
        public Task<int> CountAsync(string? search, bool? isActive, int? departmentId, CancellationToken cancellationToken = default)
            => _inner.CountAsync(search, isActive, departmentId, cancellationToken);
        public Task<bool> ExistsByPayrollNumberAsync(string payrollNumber, CancellationToken cancellationToken = default)
            => _inner.ExistsByPayrollNumberAsync(payrollNumber, cancellationToken);
        public Task<List<string>> GetExistingPayrollNumbersAsync(IEnumerable<string> payrollNumbers, CancellationToken cancellationToken = default)
            => _inner.GetExistingPayrollNumbersAsync(payrollNumbers, cancellationToken);
        public Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return _inner.AddAsync(employee, cancellationToken);
        }
        public Task AddRangeAsync(IEnumerable<Employee> employees, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            return _inner.AddRangeAsync(employees, cancellationToken);
        }
        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return _inner.UpdateAsync(employee, cancellationToken);
        }
    }
}
