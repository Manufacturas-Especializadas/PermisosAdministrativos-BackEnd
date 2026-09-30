using Microsoft.Extensions.DependencyInjection;
using PermisosAdministrativos.Application.Features.Departments.Commands.CreateDepartment;
using PermisosAdministrativos.Application.Features.Departments.Queries.GetDepartments;
using PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;
using PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;
using PermisosAdministrativos.Application.Features.Employees.Commands.SetEmployeeStatus;
using PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployees;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.ApprovePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CompletePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CreatePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.RejectPersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetApprovedPersonalPermits;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPendingPersonalPermits;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPersonalPermits;
using PermisosAdministrativos.Application.Features.Users.Commands.CreateUser;
using PermisosAdministrativos.Application.Features.Users.Queries.GetUsers;

namespace PermisosAdministrativos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreatePersonalPermitCommandHandler>();
        services.AddScoped<GetEmployeesQueryHandler>();
        services.AddScoped<CreateEmployeeCommandHandler>();
        services.AddScoped<CreateDepartmentCommandHandler>();
        services.AddScoped<GetDepartmentsQueryHandler>();
        services.AddScoped<GetPendingPersonalPermitsQueryHandler>();
        services.AddScoped<ApprovePersonalPermitCommandHandler>();
        services.AddScoped<RejectPersonalPermitCommandHandler>();
        services.AddScoped<GetApprovedPersonalPermitsQueryHandler>();
        services.AddScoped<CompletePersonalPermitCommandHandler>();
        services.AddScoped<GetPersonalPermitsQueryHandler>();
        services.AddScoped<CreateUserCommandHandler>();
        services.AddScoped<GetUsersQueryHandler>();
        services.AddScoped<ImportEmployeesCommandHandler>();
        services.AddScoped<SetEmployeeStatusCommandHandler>();

        return services;
    }
}