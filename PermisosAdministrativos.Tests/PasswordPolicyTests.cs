using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Infrastructure.Identity;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class PasswordPolicyTests
{
    public static IEnumerable<object[]> ValidPasswords()
    {
        yield return ["abcd"];
        yield return ["ABCD"];
        yield return ["1234"];
        yield return ["!!!!"];
        yield return ["aaaa"];
        yield return [new string('a', 64)];
        yield return [" ab "];
    }

    [Fact]
    public void IdentityOptionsUseOnlyRequestedRequirements()
    {
        using var fixture = new AuthTestFixture();
        var options = fixture.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Password;
        Assert.Equal(4, options.RequiredLength);
        Assert.False(options.RequireDigit);
        Assert.False(options.RequireUppercase);
        Assert.False(options.RequireLowercase);
        Assert.False(options.RequireNonAlphanumeric);
    }

    [Theory]
    [MemberData(nameof(ValidPasswords))]
    public async Task CreationAcceptsSimplePasswordsAndMaximumLength(string password)
    {
        using var fixture = new AuthTestFixture();
        var roles = fixture.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(Roles.Security))).Succeeded);
        var id = await fixture.Services.GetRequiredService<IIdentityService>()
            .CreateUserAsync("test.user", password, null, Roles.Security);
        var user = await fixture.Users.FindByIdAsync(id);
        Assert.NotNull(user);
        Assert.False(user.MustChangePassword);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(await fixture.Users.CheckPasswordAsync(user, password));
    }

    [Theory]
    [InlineData(0, "PasswordTooShort")]
    [InlineData(3, "PasswordTooShort")]
    [InlineData(65, "PasswordTooLong")]
    public async Task IdentityCreationRejectsOutOfRangePasswords(int length, string code)
    {
        using var fixture = new AuthTestFixture();
        var user = new ApplicationUser { UserName = "test.user" };
        var result = await fixture.Users.CreateAsync(user, new string('a', length));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == code);
        Assert.Null(await fixture.Users.FindByNameAsync("test.user"));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(65)]
    public async Task ExistingCreationServiceAlsoRejectsOutOfRangePasswords(int length)
    {
        using var fixture = new AuthTestFixture();
        var roles = fixture.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(Roles.Security))).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Services.GetRequiredService<IIdentityService>()
                .CreateUserAsync("test.user", new string('a', length), null, Roles.Security));
        Assert.Null(await fixture.Users.FindByNameAsync("test.user"));
    }
}
