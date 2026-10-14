using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployees;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Infrastructure.Repositories;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class GetEmployeesTests
{
    private static async Task SeedEmployees(EmployeeTestContext context)
    {
        await context.SeedAsync(false);
        context.Employees.AddRange(
            new Employee { Id = 30, PayrollNumber = "N030", FullName = "Ana", DepartmentId = 1, IsActive = true },
            new Employee { Id = 10, PayrollNumber = "N010", FullName = "Ana", DepartmentId = 2, IsActive = false },
            new Employee { Id = 20, PayrollNumber = "N020", FullName = "Ana", DepartmentId = 1, IsActive = true },
            new Employee { Id = 40, PayrollNumber = "N040", FullName = "Bruno", DepartmentId = 2, IsActive = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        context.SaveCalls = 0;
    }

    [Fact]
    public async Task SameNamesHaveStableIdOrderAcrossPages()
    {
        using var context = new EmployeeTestContext();
        await SeedEmployees(context);
        var handler = new GetEmployeesQueryHandler(new EmployeeRepository(context));
        var first = await handler.HandleAsync(new(null, null, null, 1, 2));
        var second = await handler.HandleAsync(new(null, null, null, 2, 2));
        Assert.Equal(new[] { 10, 20 }, first.Items.Select(x => x.Id));
        Assert.Equal(new[] { 30, 40 }, second.Items.Select(x => x.Id));
        Assert.Equal(4, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(2, second.Page);
        Assert.Equal("Producción", first.Items[0].DepartmentName);
        Assert.Equal("Calidad", first.Items[1].DepartmentName);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(" Ana ", null, null, 3)]
    [InlineData("N020", null, null, 1)]
    [InlineData("   ", null, null, 4)]
    [InlineData(null, true, null, 3)]
    [InlineData(null, false, null, 1)]
    [InlineData(null, null, 1, 2)]
    [InlineData("Ana", true, 1, 2)]
    [InlineData("Ana", false, 1, 0)]
    [InlineData("No existe", null, null, 0)]
    public async Task SearchAndFiltersMatchTotalCount(string? search, bool? active, int? department, int expected)
    {
        using var context = new EmployeeTestContext();
        await SeedEmployees(context);
        var handler = new GetEmployeesQueryHandler(new EmployeeRepository(context));
        var result = await handler.HandleAsync(new(search, active, department, 1, 100));
        Assert.Equal(expected, result.TotalCount);
        Assert.Equal(expected, result.Items.Count);
        Assert.Equal(expected == 0 ? 0 : 1, result.TotalPages);
        if (search?.Trim() == "N020")
            Assert.Equal(20, Assert.Single(result.Items).Id);
        if (active.HasValue)
            Assert.All(result.Items, item => Assert.Equal(active.Value, item.IsActive));
        if (department.HasValue)
            Assert.All(result.Items, item => Assert.Equal(department.Value, item.DepartmentId));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-1, 25)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task RejectsInvalidPagination(int page, int size)
    {
        using var context = new EmployeeTestContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new GetEmployeesQueryHandler(new EmployeeRepository(context)).HandleAsync(new(null, null, null, page, size)));
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(42949674, 100)]
    [InlineData(int.MaxValue, 100)]
    public async Task BeyondLastPageIncludingOverflowReturnsEmptyWithCorrectTotals(int page, int size)
    {
        using var context = new EmployeeTestContext();
        await SeedEmployees(context);
        var result = await new GetEmployeesQueryHandler(new EmployeeRepository(context))
            .HandleAsync(new(null, null, null, page, size));
        Assert.Empty(result.Items);
        Assert.Equal(page, result.Page);
        Assert.Equal(size, result.PageSize);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal((int)Math.Ceiling(4d / size), result.TotalPages);
    }
}
