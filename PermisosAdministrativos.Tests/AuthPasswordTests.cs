using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Application.DTOs.Auth;
using PermisosAdministrativos.Infrastructure.Identity;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class AuthPasswordTests
{
    [Fact]
    public void NewUserDefaultsToNotRequiringPasswordChange()
    {
        Assert.False(new ApplicationUser().MustChangePassword);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MeReturnsPersistedFlagAndPreservesExistingFields(bool mustChange)
    {
        using var fixture = new AuthTestFixture();
        var user = await fixture.CreateUserAsync(mustChange: mustChange);
        var roles = fixture.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Security"))).Succeeded);
        Assert.True((await fixture.Users.AddToRoleAsync(user, "Security")).Succeeded);
        await fixture.SignInAsync(user);
        fixture.Database.ChangeTracker.Clear();
        var response = Assert.IsType<OkObjectResult>(await fixture.Controller().Me());
        var json = JsonSerializer.SerializeToElement(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "mustChangePassword", "roles", "userId", "userName" },
            json.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal(user.Id, json.GetProperty("userId").GetString());
        Assert.Equal(user.UserName, json.GetProperty("userName").GetString());
        Assert.Equal("Security", Assert.Single(json.GetProperty("roles").EnumerateArray()).GetString());
        Assert.Equal(mustChange, json.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task SuccessfulChangeResetsFlagUpdatesStampAndRefreshesSession()
    {
        using var fixture = new AuthTestFixture();
        var user = await fixture.CreateUserAsync(mustChange: true);
        await fixture.SignInAsync(user);
        var previousPrincipal = fixture.Http.User;
        var oldStamp = user.SecurityStamp;
        var oldHash = user.PasswordHash;
        Assert.IsType<NoContentResult>(await fixture.Controller().ChangePassword(new("oldpass", "abcd")));
        var refreshedCookie = fixture.ResponseCookie();
        Assert.NotEmpty(refreshedCookie);
        fixture.Database.ChangeTracker.Clear();
        var saved = await fixture.Users.FindByIdAsync(user.Id);
        Assert.NotNull(saved);
        Assert.False(saved.MustChangePassword);
        Assert.NotEqual(oldStamp, saved.SecurityStamp);
        Assert.NotEqual(oldHash, saved.PasswordHash);
        Assert.NotEqual("abcd", saved.PasswordHash);
        Assert.True(await fixture.Users.CheckPasswordAsync(saved, "abcd"));
        Assert.False(await fixture.Users.CheckPasswordAsync(saved, "oldpass"));
        Assert.Null(await fixture.SignIn.ValidateSecurityStampAsync(previousPrincipal));

        fixture.StartRequest(refreshedCookie);
        var authentication = await fixture.Http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        Assert.True(authentication.Succeeded);
        Assert.NotNull(await fixture.SignIn.ValidateSecurityStampAsync(authentication.Principal));
        fixture.Http.User = authentication.Principal!;
        var me = Assert.IsType<OkObjectResult>(await fixture.Controller().Me());
        Assert.False(JsonSerializer.SerializeToElement(me.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("mustChangePassword").GetBoolean());

        fixture.StartRequest();
        Assert.IsType<UnauthorizedObjectResult>(await fixture.Controller().Login(new("test.user", "oldpass")));
        fixture.StartRequest();
        Assert.IsType<OkResult>(await fixture.Controller().Login(new("test.user", "abcd")));
    }

    [Theory]
    [MemberData(nameof(PasswordPolicyTests.ValidPasswords), MemberType = typeof(PasswordPolicyTests))]
    public async Task ChangeAcceptsSamePasswordsAsCreation(string password)
    {
        using var fixture = new AuthTestFixture();
        var user = await fixture.CreateUserAsync(mustChange: true);
        await fixture.SignInAsync(user);
        Assert.IsType<NoContentResult>(await fixture.Controller().ChangePassword(new("oldpass", password)));
        fixture.Database.ChangeTracker.Clear();
        var saved = await fixture.Users.FindByIdAsync(user.Id);
        Assert.NotNull(saved);
        Assert.False(saved.MustChangePassword);
        Assert.True(await fixture.Users.CheckPasswordAsync(saved, password));
    }

    [Theory]
    [InlineData(true, "wrong-current", "PasswordMismatch")]
    [InlineData(false, "wrong-current", "PasswordMismatch")]
    [InlineData(true, "short-new", "PasswordTooShort")]
    [InlineData(false, "short-new", "PasswordTooShort")]
    [InlineData(true, "long-new", "PasswordTooLong")]
    [InlineData(false, "long-new", "PasswordTooLong")]
    public async Task FailedChangeReturnsIdentityErrorsAndPreservesPasswordFlagAndStamp(bool mustChange, string scenario, string code)
    {
        using var fixture = new AuthTestFixture();
        var user = await fixture.CreateUserAsync(mustChange: mustChange);
        await fixture.SignInAsync(user);
        var oldHash = user.PasswordHash;
        var oldStamp = user.SecurityStamp;
        var request = new ChangePasswordRequest(scenario == "wrong-current" ? "wrong" : "oldpass",
            scenario == "short-new" ? "abc" : scenario == "long-new" ? new string('a', 65) : "abcd");
        var response = Assert.IsType<BadRequestObjectResult>(await fixture.Controller().ChangePassword(request));
        var problem = Assert.IsType<ValidationProblemDetails>(response.Value);
        Assert.Equal(400, problem.Status);
        Assert.True(problem.Errors.ContainsKey(code));
        Assert.NotEmpty(problem.Errors[code]);
        Assert.Equal(mustChange, user.MustChangePassword);
        fixture.Database.ChangeTracker.Clear();
        var saved = await fixture.Users.FindByIdAsync(user.Id);
        Assert.NotNull(saved);
        Assert.Equal(mustChange, saved.MustChangePassword);
        Assert.Equal(oldHash, saved.PasswordHash);
        Assert.Equal(oldStamp, saved.SecurityStamp);
        Assert.True(await fixture.Users.CheckPasswordAsync(saved, "oldpass"));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("missing-claim")]
    [InlineData("missing-user")]
    public async Task UnauthenticatedOrUnresolvableUserCannotReadMeOrChangePassword(string scenario)
    {
        using var fixture = new AuthTestFixture();
        if (scenario != "anonymous")
            fixture.Http.User = new ClaimsPrincipal(new ClaimsIdentity(
                scenario == "missing-user" ? [new Claim(ClaimTypes.NameIdentifier, "not-found")] : [], "Test"));
        Assert.IsType<UnauthorizedResult>(await fixture.Controller().Me());
        Assert.IsType<UnauthorizedResult>(await fixture.Controller().ChangePassword(new("oldpass", "abcd")));
    }

    [Fact]
    public async Task PayloadCannotSelectAnotherUserEvenWithTheirCurrentPassword()
    {
        using var fixture = new AuthTestFixture();
        var current = await fixture.CreateUserAsync(mustChange: true);
        var other = await fixture.CreateUserAsync("otherpass", true, "other.user");
        var otherHash = other.PasswordHash;
        var otherStamp = other.SecurityStamp;
        await fixture.SignInAsync(current);

        var request = JsonSerializer.Deserialize<ChangePasswordRequest>(JsonSerializer.Serialize(new
        {
            userId = other.Id,
            currentPassword = "otherpass",
            newPassword = "abcd"
        }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.IsType<BadRequestObjectResult>(await fixture.Controller().ChangePassword(request));
        Assert.IsType<NoContentResult>(await fixture.Controller().ChangePassword(request with { CurrentPassword = "oldpass" }));
        fixture.Database.ChangeTracker.Clear();
        var savedOther = await fixture.Users.FindByIdAsync(other.Id);
        Assert.NotNull(savedOther);
        Assert.Equal(otherHash, savedOther.PasswordHash);
        Assert.Equal(otherStamp, savedOther.SecurityStamp);
        Assert.True(savedOther.MustChangePassword);
        Assert.True(await fixture.Users.CheckPasswordAsync(savedOther, "otherpass"));
    }

    [Theory]
    [InlineData("ChangePassword")]
    [InlineData("Me")]
    public async Task AuthEndpointsRequireAuthenticationWithoutRestrictingRoles(string action)
    {
        using var fixture = new AuthTestFixture();
        var method = typeof(AuthController).GetMethod(action)!;
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Null(authorization.Roles);
        var policy = await AuthorizationPolicy.CombineAsync(
            fixture.Services.GetRequiredService<IAuthorizationPolicyProvider>(), [authorization]);
        Assert.NotNull(policy);
        var service = fixture.Services.GetRequiredService<IAuthorizationService>();
        Assert.False((await service.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
        Assert.True((await service.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity([], "Test")), null, policy)).Succeeded);
    }

    [Fact]
    public void ChangePasswordContractContainsOnlyRequiredPasswordsAndNoUserId()
    {
        Assert.Equal(new[] { "CurrentPassword", "NewPassword" },
            typeof(ChangePasswordRequest).GetProperties().Select(property => property.Name).OrderBy(name => name));
        var action = typeof(AuthController).GetMethod(nameof(AuthController.ChangePassword))!;
        Assert.Equal("change-password", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Single(action.GetParameters());
        Assert.Empty(action.GetCustomAttributes<IgnoreAntiforgeryTokenAttribute>());
        Assert.Empty(typeof(AuthController).GetCustomAttributes<IgnoreAntiforgeryTokenAttribute>());
        var invalid = new ChangePasswordRequest(null!, null!);
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), errors, true));
        Assert.Equal(2, errors.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginKeepsEmpty200ResponseAndDoesNotChangeFlag(bool mustChange)
    {
        using var fixture = new AuthTestFixture();
        var user = await fixture.CreateUserAsync("ValidPassword123", mustChange);
        Assert.IsType<OkResult>(await fixture.Controller().Login(new("test.user", "ValidPassword123")));
        Assert.Equal(mustChange, user.MustChangePassword);
        Assert.NotEmpty(fixture.ResponseCookie());
    }

    [Fact]
    public async Task ExistingPasswordLongerThanNewLimitStillAllowsLogin()
    {
        using var fixture = new AuthTestFixture();
        var oldPassword = new string('A', 70) + "a1";
        var user = new ApplicationUser { UserName = "existing.user" };
        user.PasswordHash = fixture.Users.PasswordHasher.HashPassword(user, oldPassword);
        Assert.True((await fixture.Users.CreateAsync(user)).Succeeded);
        Assert.IsType<OkResult>(await fixture.Controller().Login(new("existing.user", oldPassword)));
        Assert.False(user.MustChangePassword);
    }
}
