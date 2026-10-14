using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Employees.Commands.UpdateEmployee;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Infrastructure.Repositories;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class UpdateEmployeeTests
{
    [Theory]
    [InlineData(true, "001")]
    [InlineData(false, "001")]
    [InlineData(true, "002")]
    [InlineData(false, "002")]
    public async Task UpdatesFieldsAllowsOwnPayrollAndPreservesIdAndStatus(bool active, string payroll)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(employeeActive: active);
        var handler = new UpdateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        await handler.HandleAsync(new(1, $" {payroll} ", " María López ", 2));
        Assert.Equal(1, context.SaveCalls);
        context.ChangeTracker.Clear();
        var employee = await context.Employees.SingleAsync();
        Assert.Equal(1, employee.Id);
        Assert.Equal(payroll, employee.PayrollNumber);
        Assert.Equal("María López", employee.FullName);
        Assert.Equal(2, employee.DepartmentId);
        Assert.Equal(active, employee.IsActive);
    }

    [Fact]
    public async Task AcceptsMaximumLengthsAfterTrimming()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        var handler = new UpdateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        await handler.HandleAsync(new(1, $" {new string('N', 30)} ", $" {new string('A', 200)} ", 1));
        context.ChangeTracker.Clear();
        var employee = await context.Employees.SingleAsync();
        Assert.Equal(30, employee.PayrollNumber.Length);
        Assert.Equal(200, employee.FullName.Length);
    }

    [Theory]
    [MemberData(nameof(CreateEmployeeTests.InvalidFields), MemberType = typeof(CreateEmployeeTests))]
    public async Task UsesSameFieldValidationAsCreateWithoutMutatingEmployee(string? payroll, string? name, string message)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        var handler = new UpdateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        var error = await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new(1, payroll!, name!, 2)));
        Assert.Equal(message, error.Message);
        await AssertUnchanged(context);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CannotTakeAnotherEmployeesPayroll(bool otherActive)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        context.Employees.Add(new Employee { Id = 2, PayrollNumber = "002", FullName = "Otro", DepartmentId = 1, IsActive = otherActive });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.SaveCalls = 0;
        var handler = new UpdateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new(1, " 002 ", "Modificado", 2)));
        await AssertUnchanged(context);
        Assert.Equal(2, await context.Employees.CountAsync());
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(3, true)]
    public async Task RejectsMissingOrInactiveDepartmentBeforeMutation(int departmentId, bool exists)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        var handler = new UpdateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        var error = await Record.ExceptionAsync(() => handler.HandleAsync(new(1, "002", "Nuevo", departmentId)));
        if (exists)
            Assert.IsType<ValidationException>(error);
        else
            Assert.IsType<NotFoundException>(error);
        await AssertUnchanged(context);
    }

    [Fact]
    public async Task MissingEmployeeIsNotFound()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        var handler = new UpdateEmployeeCommandHandler(new EmployeeRepository(context), new DepartmentRepository(context));
        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new(99, "002", "Ana", 1)));
        Assert.Equal("El empleado no existe.", error.Message);
        Assert.Equal(0, context.SaveCalls);
    }

    private static async Task AssertUnchanged(EmployeeTestContext context)
    {
        Assert.Equal(0, context.SaveCalls);
        var tracked = await context.Employees.FindAsync(1);
        Assert.NotNull(tracked);
        Assert.Equal("001", tracked.PayrollNumber);
        Assert.Equal("Ana", tracked.FullName);
        Assert.Equal(1, tracked.DepartmentId);
        context.ChangeTracker.Clear();
        var saved = await context.Employees.SingleAsync(x => x.Id == 1);
        Assert.Equal("001", saved.PayrollNumber);
        Assert.Equal("Ana", saved.FullName);
        Assert.Equal(1, saved.DepartmentId);
        Assert.True(saved.IsActive);
    }
}
