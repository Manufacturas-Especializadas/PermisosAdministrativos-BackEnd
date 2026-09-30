using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Application.Authorization;

namespace PermisosAdministrativos.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        string? adminUserName,
        string? adminPassword)
    {
        using var scope = services.CreateScope();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[]
        {
            Roles.Administrator,
            Roles.Supervisor,
            Roles.HumanResources,
            Roles.Security
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (string.IsNullOrWhiteSpace(adminUserName) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var admin = await userManager.FindByNameAsync(adminUserName);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminUserName
            };

            var result = await userManager.CreateAsync(
                admin,
                adminPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"No se pudo crear el administrador: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(
                admin,
                Roles.Administrator))
        {
            await userManager.AddToRoleAsync(
                admin,
                Roles.Administrator);
        }
    }
}