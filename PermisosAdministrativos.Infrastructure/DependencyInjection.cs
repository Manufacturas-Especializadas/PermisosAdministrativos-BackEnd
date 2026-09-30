using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Interfaces;
using PermisosAdministrativos.Infrastructure.Identity;
using PermisosAdministrativos.Infrastructure.Persistence;
using PermisosAdministrativos.Infrastructure.Repositories;
using PermisosAdministrativos.Infrastructure.Services;

namespace PermisosAdministrativos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {


        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;

            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IPersonalPermitRepository, PersonalPermitRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IEmployeeSpreadsheetReader, EmployeeSpreadsheetReader>();

        return services;
    }
}