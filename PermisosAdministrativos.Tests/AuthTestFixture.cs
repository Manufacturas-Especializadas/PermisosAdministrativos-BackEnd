using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Api.Services;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Infrastructure;
using PermisosAdministrativos.Infrastructure.Identity;
using PermisosAdministrativos.Infrastructure.Persistence;
using Xunit;

namespace PermisosAdministrativos.Tests;

internal sealed class AuthTestFixture : IDisposable
{
    private readonly ServiceProvider _provider;
    private IServiceScope _scope;
    public IServiceProvider Services => _scope.ServiceProvider;
    public UserManager<ApplicationUser> Users => Services.GetRequiredService<UserManager<ApplicationUser>>();
    public SignInManager<ApplicationUser> SignIn
    {
        get
        {
            var manager = Services.GetRequiredService<SignInManager<ApplicationUser>>();
            manager.Context = Http;
            return manager;
        }
    }
    public ApplicationDbContext Database => Services.GetRequiredService<ApplicationDbContext>();
    public DefaultHttpContext Http { get; private set; }

    public AuthTestFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddInfrastructure("Server=unused;Database=unused");
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        Http = NewHttpContext();
    }

    private DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext { RequestServices = Services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        Services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        return context;
    }

    public void StartRequest(string? cookie = null)
    {
        _scope.Dispose();
        _scope = _provider.CreateScope();
        Http = NewHttpContext();
        if (cookie is not null)
            Http.Request.Headers.Cookie = cookie;
    }

    public string ResponseCookie() => string.Join("; ", Http.Response.Headers.SetCookie
        .Select(value => value!.Split(';')[0]));

    public AuthController Controller()
    {
        Services.GetRequiredService<IHttpContextAccessor>().HttpContext = Http;
        return new AuthController(SignIn, Users, Services.GetRequiredService<ICurrentUserService>())
        {
            ControllerContext = new ControllerContext { HttpContext = Http }
        };
    }

    public async Task<ApplicationUser> CreateUserAsync(
        string password = "oldpass",
        bool mustChange = false,
        string name = "test.user")
    {
        var user = new ApplicationUser { UserName = name, MustChangePassword = mustChange };
        var result = await Users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        return user;
    }

    public async Task SignInAsync(ApplicationUser user)
    {
        await SignIn.SignInAsync(user, isPersistent: false);
        var cookie = ResponseCookie();
        StartRequest(cookie);
        var authentication = await Http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        Assert.True(authentication.Succeeded, authentication.Failure?.Message ?? "Cookie authentication did not succeed.");
        Http.User = authentication.Principal!;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
