using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Api.ExceptionHandling;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.ApprovePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.RejectPersonalPermit;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Enums;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class PersonalPermitSelfReviewTests
{
    private static readonly DateTimeOffset ReviewTime = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static async Task<PersonalPermit> SeedAsync(AuthTestFixture fixture, int? currentEmployeeId, bool createdByCurrentUser)
    {
        fixture.Database.Departments.Add(new Department { Id = 1, Name = "Calidad" });
        fixture.Database.Employees.AddRange(
            new Employee { Id = 1, PayrollNumber = "001", FullName = "Empleado A", DepartmentId = 1 },
            new Employee { Id = 2, PayrollNumber = "002", FullName = "Empleado B", DepartmentId = 1 });
        await fixture.Database.SaveChangesAsync();
        var user = await fixture.CreateUserAsync();
        user.EmployeeId = currentEmployeeId;
        Assert.True((await fixture.Users.UpdateAsync(user)).Succeeded);

        fixture.Http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Role, "HumanResources"),
            // The persisted Identity association must win over a stale/custom employee claim.
            new Claim("EmployeeId", "999")
        ], "Test"));
        fixture.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = fixture.Http;

        var permit = new PersonalPermit
        {
            EmployeeId = 1,
            PermitDate = new DateOnly(2026, 10, 15),
            ExitTime = new TimeOnly(13, 0),
            Reason = "Trámite personal",
            Status = PermitStatus.PendingHumanResourcesApproval,
            CreatedByUserId = createdByCurrentUser ? user.Id : "another-supervisor",
            SupervisorApprovedByUserId = "supervisor",
            CreatedAt = ReviewTime.AddHours(-1).UtcDateTime,
            SupervisorApprovedAt = ReviewTime.AddHours(-1).UtcDateTime
        };
        fixture.Database.PersonalPermits.Add(permit);
        await fixture.Database.SaveChangesAsync();
        fixture.Database.ChangeTracker.Clear();
        return permit;
    }

    private static Task ReviewAsync(AuthTestFixture fixture, RecordingRepository repository, int id, bool approve)
    {
        fixture.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = fixture.Http;
        var currentUser = fixture.Services.GetRequiredService<ICurrentUserService>();
        return approve
            ? new ApprovePersonalPermitCommandHandler(repository, currentUser, new ReviewClock())
                .HandleAsync(new ApprovePersonalPermitCommand(id))
            : new RejectPersonalPermitCommandHandler(repository, currentUser, new ReviewClock())
                .HandleAsync(new RejectPersonalPermitCommand(id, "  Motivo del rechazo  "));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnRequestIsRejectedWith400WithoutChangingStateOrReviewFields(bool approve)
    {
        using var fixture = new AuthTestFixture();
        var permit = await SeedAsync(fixture, currentEmployeeId: 1, createdByCurrentUser: false);
        var repository = new RecordingRepository(fixture.Database);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ReviewAsync(fixture, repository, permit.Id, approve));

        Assert.Equal("No puedes aprobar o rechazar tu propia solicitud.", error.Message);
        Assert.Equal(0, repository.ApproveCalls);
        Assert.Equal(0, repository.RejectCalls);
        var tracked = await fixture.Database.PersonalPermits.FindAsync(permit.Id);
        Assert.NotNull(tracked);
        Assert.Equal(PermitStatus.PendingHumanResourcesApproval, tracked.Status);
        Assert.Null(tracked.HumanResourcesReviewedAt);
        Assert.Null(tracked.HumanResourcesReviewedByUserId);
        Assert.Null(tracked.RejectionReason);
        Assert.False(fixture.Database.ChangeTracker.HasChanges());

        fixture.Database.ChangeTracker.Clear();
        var saved = await fixture.Database.PersonalPermits.AsNoTracking().SingleAsync(x => x.Id == permit.Id);
        Assert.Equal(PermitStatus.PendingHumanResourcesApproval, saved.Status);
        Assert.Null(saved.HumanResourcesReviewedAt);
        Assert.Null(saved.HumanResourcesReviewedByUserId);
        Assert.Null(saved.RejectionReason);
        Assert.Equal(permit.CreatedByUserId, saved.CreatedByUserId);
        Assert.Equal(permit.SupervisorApprovedAt, saved.SupervisorApprovedAt);

        using var response = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = fixture.Services };
        http.Response.Body = response;
        Assert.True(await new GlobalExceptionHandler().TryHandleAsync(http, error, default));
        Assert.Equal(400, http.Response.StatusCode);
        response.Position = 0;
        using var json = await JsonDocument.ParseAsync(response);
        Assert.Equal(error.Message, json.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 2)]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public async Task OtherEmployeeOrUnassociatedUserKeepsExistingReviewBehavior(bool approve, int? currentEmployeeId)
    {
        using var fixture = new AuthTestFixture();
        // Creating a request for someone else does not make it the reviewer's own request.
        var permit = await SeedAsync(fixture, currentEmployeeId, createdByCurrentUser: true);
        var repository = new RecordingRepository(fixture.Database);

        await ReviewAsync(fixture, repository, permit.Id, approve);

        Assert.Equal(approve ? 1 : 0, repository.ApproveCalls);
        Assert.Equal(approve ? 0 : 1, repository.RejectCalls);
        Assert.Equal(permit.Id, repository.ReviewedPermitId);
        Assert.Equal(permit.CreatedByUserId, repository.ReviewerId);
        Assert.Equal(ReviewTime.UtcDateTime, repository.ReviewedAt);
        Assert.Equal(approve ? null : "Motivo del rechazo", repository.RejectionReason);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task AlreadyProcessedOrConcurrentReviewStillReturnsConflict(bool approve, bool alreadyProcessed)
    {
        using var fixture = new AuthTestFixture();
        var permit = await SeedAsync(fixture, currentEmployeeId: 2, createdByCurrentUser: false);
        if (alreadyProcessed)
        {
            var saved = await fixture.Database.PersonalPermits.FindAsync(permit.Id);
            saved!.Status = PermitStatus.Approved;
            await fixture.Database.SaveChangesAsync();
        }
        var repository = new RecordingRepository(fixture.Database) { ReviewSucceeded = false };
        await Assert.ThrowsAsync<ConflictException>(() => ReviewAsync(fixture, repository, permit.Id, approve));
        Assert.Equal(alreadyProcessed ? 0 : 1, repository.ApproveCalls + repository.RejectCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingPermitKeepsExistingErrorAndDoesNotWrite(bool approve)
    {
        using var fixture = new AuthTestFixture();
        var repository = new RecordingRepository(fixture.Database);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReviewAsync(fixture, repository, 99, approve));
        Assert.Equal("El permiso no existe.", error.Message);
        Assert.Equal(0, repository.ApproveCalls + repository.RejectCalls);
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    public void EndpointAuthorizationRemainsHumanResourcesAndAdministrator(string action)
    {
        var authorization = typeof(PersonalPermitsController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorization);
        Assert.Equal("HumanResources,Administrator", authorization.Roles);
    }

    private sealed class ReviewClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ReviewTime;
    }

    private sealed class RecordingRepository(ApplicationDbContext context) : IPersonalPermitRepository
    {
        public Task<List<PersonalPermit>> GetPagedByCreatorAsync(string createdByUserId, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountByCreatorAsync(string createdByUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public int ApproveCalls { get; private set; }
        public int RejectCalls { get; private set; }
        public int ReviewedPermitId { get; private set; }
        public string? ReviewerId { get; private set; }
        public string? RejectionReason { get; private set; }
        public DateTime ReviewedAt { get; private set; }
        public bool ReviewSucceeded { get; init; } = true;

        public Task<PersonalPermit?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => context.PersonalPermits.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<bool> TryApproveAsync(int id, string userId, DateTime reviewedAt, CancellationToken cancellationToken = default)
        {
            ApproveCalls++;
            ReviewedPermitId = id;
            ReviewerId = userId;
            ReviewedAt = reviewedAt;
            return Task.FromResult(ReviewSucceeded);
        }

        public Task<bool> TryRejectAsync(int id, string userId, string reason, DateTime reviewedAt, CancellationToken cancellationToken = default)
        {
            RejectCalls++;
            ReviewedPermitId = id;
            ReviewerId = userId;
            RejectionReason = reason;
            ReviewedAt = reviewedAt;
            return Task.FromResult(ReviewSucceeded);
        }

        public Task AddAsync(PersonalPermit permit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(PersonalPermit permit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<PersonalPermit>> GetPendingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryCompleteAsync(int id, string userId, DateTime actualExitAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<PersonalPermit>> GetApprovedAsync(DateOnly permitDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<PersonalPermit>> GetPagedAsync(int? employeeId, PermitStatus? status, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountAsync(int? employeeId, PermitStatus? status, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
