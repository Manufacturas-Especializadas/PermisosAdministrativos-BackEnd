using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Api.Authorization;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Common.Models;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetMyPersonalPermits;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class MyPersonalPermitsControllerTests
{
    [Theory]
    [InlineData(false, Roles.Supervisor, false, true, false)]
    [InlineData(true, Roles.Supervisor, false, true, true)]
    [InlineData(true, Roles.Administrator, false, true, true)]
    [InlineData(true, Roles.HumanResources, false, true, false)]
    [InlineData(true, Roles.Security, false, true, false)]
    [InlineData(true, "", false, true, false)]
    [InlineData(true, Roles.Supervisor, true, true, false)]
    [InlineData(true, Roles.Administrator, true, true, false)]
    [InlineData(true, Roles.Supervisor, false, false, false)]
    public async Task CombinedEndpointPolicyRequiresAuthenticationAllowedRoleAndPasswordChanged(
        bool authenticated, string role, bool mustChange, bool active, bool allowed)
    {
        using var fixture = new AuthTestFixture();
        var user = await fixture.CreateUserAsync(mustChange: mustChange);
        user.IsActive = active;
        await fixture.Database.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, role)],
            authenticated ? "Test" : null));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy(Policies.PasswordChanged,
            policy => policy.RequireAuthenticatedUser().AddRequirements(new PasswordChangedRequirement())));
        services.AddSingleton<IAuthorizationHandler>(new PasswordChangedHandler(fixture.Users));
        using var provider = services.BuildServiceProvider();
        var action = typeof(PersonalPermitsController).GetMethod(nameof(PersonalPermitsController.GetMy))!;
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(typeof(PersonalPermitsController).GetCustomAttributes<AllowAnonymousAttribute>());
        var attributes = typeof(PersonalPermitsController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
        Assert.Contains(attributes, x => x.Policy == Policies.PasswordChanged);
        Assert.Contains(attributes, x => x.Roles == "Supervisor,Administrator");
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy!);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task EndpointHasOnlyPaginationInputsAndIgnoresAttemptToSelectAnotherUser()
    {
        using var fixture = new AuthTestFixture();
        await GetMyPersonalPermitsTests.SeedAsync(fixture);
        fixture.Http.Request.QueryString = new QueryString("?userId=another-creator");
        var controller = new PersonalPermitsController(null!, null!, null!, null!, null!, null!, null!,
            GetMyPersonalPermitsTests.Handler(fixture))
        {
            ControllerContext = new ControllerContext { HttpContext = fixture.Http }
        };
        var action = typeof(PersonalPermitsController).GetMethod(nameof(PersonalPermitsController.GetMy))!;
        Assert.Equal("my", action.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(new[] { "page", "pageSize", "cancellationToken" }, action.GetParameters().Select(x => x.Name));
        Assert.Equal(new[] { "Page", "PageSize" }, typeof(GetMyPersonalPermitsQuery).GetProperties().Select(x => x.Name));
        var response = Assert.IsType<OkObjectResult>(await controller.GetMy());
        var result = Assert.IsType<PagedResult<MyPersonalPermitDto>>(response.Value);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.DoesNotContain(result.Items, x => x.Id == 99);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var expectedFields = new[]
        {
            "id", "employeeId", "payrollNumber", "employeeName", "permitDate", "exitTime", "permitType",
            "reason", "status", "rejectionReason", "humanResourcesReviewedAt", "actualExitAt", "createdAt"
        };
        Assert.All(json.RootElement.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(expectedFields.Order(), item.EnumerateObject().Select(x => x.Name).Order()));
    }
}
