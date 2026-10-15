using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetMyPersonalPermits;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Infrastructure.Repositories;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class GetMyPersonalPermitsTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ReviewedAt = CreatedAt.AddMinutes(15);
    private static readonly DateTime ExitAt = CreatedAt.AddHours(1);

    internal static async Task SeedAsync(AuthTestFixture fixture)
    {
        var user = await fixture.CreateUserAsync();
        fixture.Database.Departments.Add(new Department { Id = 1, Name = "Calidad" });
        fixture.Database.Employees.AddRange(
            new Employee { Id = 1, PayrollNumber = "SUP001", FullName = "Supervisor A", DepartmentId = 1 },
            new Employee { Id = 2, PayrollNumber = "EMP002", FullName = "Empleado B", DepartmentId = 1 });
        user.EmployeeId = 1;
        foreach (var status in Enum.GetValues<PermitStatus>())
        {
            fixture.Database.PersonalPermits.Add(new PersonalPermit
            {
                Id = (int)status,
                EmployeeId = status == PermitStatus.PendingHumanResourcesApproval ? 1 : 2,
                CreatedByUserId = user.Id,
                SupervisorApprovedByUserId = user.Id,
                PermitDate = new DateOnly(2026, 10, 15),
                ExitTime = new TimeOnly(13, 0),
                PermitType = PersonalPermitType.Personal,
                Reason = "Trámite personal",
                Status = status,
                // Deliberately present on other states to verify that the DTO hides it.
                RejectionReason = "Motivo del rechazo",
                HumanResourcesReviewedAt = status == PermitStatus.PendingHumanResourcesApproval ? null : ReviewedAt,
                HumanResourcesReviewedByUserId = "internal-reviewer",
                ActualExitAt = status == PermitStatus.Completed ? ExitAt : null,
                ExitRegisteredByUserId = "internal-security",
                CreatedAt = CreatedAt
            });
        }
        fixture.Database.PersonalPermits.Add(new PersonalPermit
        {
            Id = 99, EmployeeId = 1, CreatedByUserId = "another-creator",
            SupervisorApprovedByUserId = "another-creator", Reason = "Privado de otro creador",
            PermitDate = new DateOnly(2026, 10, 16), ExitTime = new TimeOnly(14, 0),
            PermitType = PersonalPermitType.Personal, Status = PermitStatus.Approved
        });
        await fixture.Database.SaveChangesAsync();
        await fixture.SignInAsync(user);
        fixture.Database.ChangeTracker.Clear();
    }

    internal static GetMyPersonalPermitsQueryHandler Handler(AuthTestFixture fixture)
    {
        fixture.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = fixture.Http;
        return new GetMyPersonalPermitsQueryHandler(
            new PersonalPermitRepository(fixture.Database), fixture.Services.GetRequiredService<ICurrentUserService>());
    }

    [Fact]
    public async Task FiltersByAuthenticatedCreatorRegardlessOfBeneficiaryIncludingTotals()
    {
        using var fixture = new AuthTestFixture();
        await SeedAsync(fixture);
        var result = await Handler(fixture).HandleAsync(new());

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, result.Items.Select(x => x.Id));
        Assert.Contains(result.Items, x => x.EmployeeId == 1);
        Assert.Contains(result.Items, x => x.EmployeeId == 2 && x.PayrollNumber == "EMP002" && x.EmployeeName == "Empleado B");
        Assert.DoesNotContain(result.Items, x => x.Id == 99);
        Assert.All(result.Items, x => Assert.Equal(CreatedAt, x.CreatedAt));
        Assert.Empty(fixture.Database.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(PermitStatus.PendingHumanResourcesApproval)]
    [InlineData(PermitStatus.Approved)]
    [InlineData(PermitStatus.Rejected)]
    [InlineData(PermitStatus.Cancelled)]
    [InlineData(PermitStatus.Completed)]
    public async Task PreservesRealStateAndReviewAndExitInformation(PermitStatus status)
    {
        using var fixture = new AuthTestFixture();
        await SeedAsync(fixture);
        var result = await Handler(fixture).HandleAsync(new());
        var item = Assert.Single(result.Items, x => x.Id == (int)status);

        Assert.Equal(status, item.Status);
        Assert.Equal(status == PermitStatus.Rejected ? "Motivo del rechazo" : null, item.RejectionReason);
        Assert.Equal(status == PermitStatus.Completed ? ExitAt : (DateTime?)null, item.ActualExitAt);
        Assert.Equal(status == PermitStatus.PendingHumanResourcesApproval ? null : (DateTime?)ReviewedAt, item.HumanResourcesReviewedAt);
        Assert.Equal(PersonalPermitType.Personal, item.PermitType);
        Assert.Equal("Trámite personal", item.Reason);
    }

    [Fact]
    public async Task PaginationUsesDateThenTimeThenIdDescendingAndCountsOnlyCreator()
    {
        using var fixture = new AuthTestFixture();
        await SeedAsync(fixture);
        var permit = await fixture.Database.PersonalPermits.FindAsync(1);
        permit!.PermitDate = new DateOnly(2026, 10, 16);
        var laterTime = await fixture.Database.PersonalPermits.FindAsync(2);
        laterTime!.ExitTime = new TimeOnly(14, 0);
        await fixture.Database.SaveChangesAsync();
        fixture.Database.ChangeTracker.Clear();

        var handler = Handler(fixture);
        var first = await handler.HandleAsync(new(1, 2));
        var second = await handler.HandleAsync(new(2, 2));
        var third = await handler.HandleAsync(new(3, 2));
        Assert.Equal(new[] { 1, 2 }, first.Items.Select(x => x.Id));
        Assert.Equal(new[] { 5, 4 }, second.Items.Select(x => x.Id));
        Assert.Equal(3, Assert.Single(third.Items).Id);
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(3, second.TotalPages);
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
    }

    [Theory]
    [InlineData(0, 25, "La página debe ser mayor a 0.")]
    [InlineData(-1, 25, "La página debe ser mayor a 0.")]
    [InlineData(1, 0, "El tamaño de página debe estar entre 1 y 100.")]
    [InlineData(1, -1, "El tamaño de página debe estar entre 1 y 100.")]
    [InlineData(1, 101, "El tamaño de página debe estar entre 1 y 100.")]
    public async Task RejectsInvalidPaginationWithExistingValidationError(int page, int pageSize, string message)
    {
        using var fixture = new AuthTestFixture();
        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(fixture).HandleAsync(new(page, pageSize)));
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData(10, 25)]
    [InlineData(int.MaxValue, 100)]
    public async Task OutOfRangePageIncludingOverflowReturnsEmptyWithFilteredTotals(int page, int pageSize)
    {
        using var fixture = new AuthTestFixture();
        await SeedAsync(fixture);
        var result = await Handler(fixture).HandleAsync(new(page, pageSize));
        Assert.Empty(result.Items);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task CreatorWithoutPermitsDoesNotReceiveOtherUsersHistory()
    {
        using var fixture = new AuthTestFixture();
        await SeedAsync(fixture);
        var other = await fixture.CreateUserAsync(name: "creator.without.permits");
        await fixture.SignInAsync(other);
        var result = await Handler(fixture).HandleAsync(new());
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }
}
