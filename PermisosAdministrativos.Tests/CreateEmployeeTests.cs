using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Infrastructure.Persistence;
using PermisosAdministrativos.Infrastructure.Repositories;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class CreateEmployeeTests
{
    public static IEnumerable<object?[]> InvalidFields()
    {
        yield return [null, "Ana", "El número de nómina es obligatorio."];
        yield return ["", "Ana", "El número de nómina es obligatorio."];
        yield return ["   ", "Ana", "El número de nómina es obligatorio."];
        yield return ["002", null, "El nombre del empleado es obligatorio."];
        yield return ["002", "", "El nombre del empleado es obligatorio."];
        yield return ["002", "   ", "El nombre del empleado es obligatorio."];
        yield return [new string('N', 31), "Ana", "El número de nómina no puede exceder 30 caracteres."];
        yield return ["002", new string('A', 201), "El nombre no puede exceder 200 caracteres."];
    }

    [Theory]
    [InlineData(3, 10)]
    [InlineData(30, 200)]
    public async Task CreatesActiveEmployeeWithTrimmedFields(int payrollLength, int nameLength)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        var handler = new CreateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        var id = await handler.HandleAsync(new($" {new string('N', payrollLength)} ", $" {new string('A', nameLength)} ", 1));
        context.ChangeTracker.Clear();
        var employee = await context.Employees.SingleAsync();
        Assert.Equal(id, employee.Id);
        Assert.Equal(new string('N', payrollLength), employee.PayrollNumber);
        Assert.Equal(new string('A', nameLength), employee.FullName);
        Assert.Equal(1, employee.DepartmentId);
        Assert.True(employee.IsActive);
        Assert.Equal(1, context.SaveCalls);
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task RejectsInvalidFieldsWithoutSaving(string? payroll, string? name, string message)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        var handler = new CreateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        var error = await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new(payroll!, name!, 1)));
        Assert.Equal(message, error.Message);
        Assert.Equal(0, context.SaveCalls);
        Assert.Empty(await context.Employees.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingPayrollConflictsEvenForInactiveEmployee(bool active)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(employeeActive: active);
        var handler = new CreateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new(" 001 ", "Otro", 1)));
        Assert.Equal(0, context.SaveCalls);
        Assert.Single(await context.Employees.ToListAsync());
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(3, true)]
    public async Task DepartmentMustExistAndBeActive(int departmentId, bool exists)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(false);
        var handler = new CreateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        var error = await Record.ExceptionAsync(() => handler.HandleAsync(new("002", "Ana", departmentId)));
        if (exists)
            Assert.Equal("El departamento está inactivo.", Assert.IsType<ValidationException>(error).Message);
        else
            Assert.Equal("El departamento no existe.", Assert.IsType<NotFoundException>(error).Message);
        Assert.Equal(0, context.SaveCalls);
        Assert.Empty(await context.Employees.ToListAsync());
    }
}

internal sealed class EmployeeTestContext() : ApplicationDbContext(
    new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
{
    public int SaveCalls { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        return base.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedAsync(bool withEmployee = true, bool employeeActive = true)
    {
        Departments.AddRange(
            new Department { Id = 1, Name = "Calidad", IsActive = true },
            new Department { Id = 2, Name = "Producción", IsActive = true },
            new Department { Id = 3, Name = "Inactivo", IsActive = false });
        if (withEmployee)
            Employees.Add(new Employee { Id = 1, PayrollNumber = "001", FullName = "Ana", DepartmentId = 1, IsActive = employeeActive });
        await SaveChangesAsync();
        ChangeTracker.Clear();
        SaveCalls = 0;
    }
}
