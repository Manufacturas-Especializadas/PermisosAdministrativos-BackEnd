using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Authorization;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Application.Authorization;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class PasswordChangedAuthorizationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PasswordChangedRequirementUsesPersistedFlag(
        bool mustChangePassword,
        bool expectedSuccess)
    {
        using var fixture = new AuthTestFixture();

        var user = await fixture.CreateUserAsync(
            mustChange: mustChangePassword);

        await fixture.SignInAsync(user);

        var requirement = new PasswordChangedRequirement();

        var context = new AuthorizationHandlerContext(
            [requirement],
            fixture.Http.User,
            null);

        var handler = new PasswordChangedHandler(fixture.Users);

        await handler.HandleAsync(context);

        Assert.Equal(
            expectedSuccess,
            context.HasSucceeded);
    }

    [Theory]
    [InlineData(typeof(EmployeesController))]
    [InlineData(typeof(DepartmentsController))]
    [InlineData(typeof(UsersController))]
    [InlineData(typeof(PersonalPermitsController))]
    public void BusinessControllersRequirePasswordChangedPolicy(
        Type controllerType)
    {
        var attributes = controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>();

        Assert.Contains(
            attributes,
            attribute =>
                attribute.Policy == Policies.PasswordChanged);
    }

    [Fact]
    public void AuthControllerDoesNotRequirePasswordChangedPolicy()
    {
        var attributes = typeof(AuthController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>();

        Assert.DoesNotContain(
            attributes,
            attribute =>
                attribute.Policy == Policies.PasswordChanged);
    }
}