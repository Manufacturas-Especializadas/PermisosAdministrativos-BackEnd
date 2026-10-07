using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Infrastructure;
using PermisosAdministrativos.Infrastructure.Identity;
using PermisosAdministrativos.Infrastructure.Persistence;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class UserNameLengthTests : IDisposable
{
    private const string Password = "ValidPassword123";
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly IIdentityService _identity;
    private readonly UserManager<ApplicationUser> _users;

    public UserNameLengthTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure("Server=unused;Database=unused");
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _identity = _scope.ServiceProvider.GetRequiredService<IIdentityService>();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    private async Task CreateRoleAsync()
    {
        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(Roles.Security))).Succeeded);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData(" abc ")]
    public async Task ThreeCharactersAreRejected(string userName)
    {
        await CreateRoleAsync();
        await Assert.ThrowsAsync<ValidationException>(() =>
            _identity.CreateUserAsync(userName, Password, null, Roles.Security));
        Assert.Empty(await _users.Users.ToListAsync());
    }

    [Theory]
    [InlineData("abcd")]
    [InlineData("abcde")]
    [InlineData("existing.user")]
    [InlineData(" abcd ")]
    public async Task ValidNamesAreCreated(string userName)
    {
        await CreateRoleAsync();
        var id = await _identity.CreateUserAsync(userName, Password, null, Roles.Security);
        var user = await _users.FindByIdAsync(id);
        Assert.NotNull(user);
        Assert.Equal(userName.Trim(), user.UserName);
        Assert.True(await _users.CheckPasswordAsync(user, Password));
    }

    [Fact]
    public async Task ExistingUserStillWorksAfterCreatingFourCharacterUser()
    {
        await CreateRoleAsync();
        var existing = new ApplicationUser { UserName = "existing.user" };
        Assert.True((await _users.CreateAsync(existing, Password)).Succeeded);
        var passwordHash = existing.PasswordHash;
        await _identity.CreateUserAsync("abcd", Password, null, Roles.Security);
        var loaded = await _users.FindByNameAsync("existing.user");
        Assert.NotNull(loaded);
        Assert.Equal(existing.Id, loaded.Id);
        Assert.Equal(passwordHash, loaded.PasswordHash);
        Assert.True(await _users.CheckPasswordAsync(loaded, Password));
    }

    [Fact]
    public void UserNameMaximumRemains256()
    {
        var context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = context.Model.FindEntityType(typeof(ApplicationUser))!;
        Assert.Equal(256, entity.FindProperty(nameof(ApplicationUser.UserName))!.GetMaxLength());
        Assert.Equal(256, entity.FindProperty(nameof(ApplicationUser.NormalizedUserName))!.GetMaxLength());
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
