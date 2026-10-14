using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployeeById;
using PermisosAdministrativos.Infrastructure.Repositories;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class GetEmployeeByIdTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReturnsCompleteDtoIncludingDepartmentWithoutTracking(bool active)
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync(employeeActive: active);
        var handler = new GetEmployeeByIdQueryHandler(new EmployeeRepository(context));
        var result = await handler.HandleAsync(new(1));
        Assert.Equal(new EmployeeDto(1, "001", "Ana", 1, "Calidad", active), result);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task MissingEmployeeIsNotFound()
    {
        using var context = new EmployeeTestContext();
        var handler = new GetEmployeeByIdQueryHandler(new EmployeeRepository(context));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new(99)));
    }

    [Fact]
    public async Task OriginalGetByIdStillTracksWithoutLoadingDepartment()
    {
        using var context = new EmployeeTestContext();
        await context.SeedAsync();
        var employee = await new EmployeeRepository(context).GetByIdAsync(1);
        Assert.NotNull(employee);
        Assert.Null(employee.Department);
        Assert.Equal(EntityState.Unchanged, context.Entry(employee).State);
        Assert.False(context.Entry(employee).Reference(x => x.Department).IsLoaded);
    }
}
