using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Employees.Commands.SetEmployeeStatus;
using PermisosAdministrativos.Infrastructure.Repositories;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class SetEmployeeStatusTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SetsStatusWithoutDeletingOrChangingOtherFields(bool initial, bool requested)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(employeeActive: initial);
        await new SetEmployeeStatusCommandHandler(new EmployeeRepository(context)).HandleAsync(new(1, requested));
        context.ChangeTracker.Clear();
        var employee = await context.Employees.SingleAsync();
        Assert.Equal(1, employee.Id);
        Assert.Equal("001", employee.PayrollNumber);
        Assert.Equal("Ana", employee.FullName);
        Assert.Equal(1, employee.DepartmentId);
        Assert.Equal(requested, employee.IsActive);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task MissingEmployeeIsNotFoundWithoutSaving()
    {
        using var context = new EmployeeTestContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SetEmployeeStatusCommandHandler(new EmployeeRepository(context)).HandleAsync(new(99, false)));
        Assert.Equal(0, context.SaveCalls);
    }
}
