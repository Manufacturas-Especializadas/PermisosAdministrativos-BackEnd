using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;

namespace PermisosAdministrativos.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<string> CreateUserAsync(
        string userName,
        string password,
        int? employeeId,
        string role)
    {
        if (!await _roleManager.RoleExistsAsync(role))
            throw new InvalidOperationException("El rol no existe.");

        var existingUser = await _userManager.FindByNameAsync(userName);

        if (existingUser is not null)
            throw new InvalidOperationException("El usuario ya existe.");

        var user = new ApplicationUser
        {
            UserName = userName.Trim(),
            EmployeeId = employeeId
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(x => x.Description));

            throw new InvalidOperationException(errors);
        }

        await _userManager.AddToRoleAsync(user, role);

        return user.Id;
    }

    public async Task SetUserActiveStatusAsync(
        string userId,
        bool isActive)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("El usuario no existe.");

        if (!isActive && user.IsActive
            && await _userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            var administrators = await _userManager.GetUsersInRoleAsync(
                Roles.Administrator);

            if (administrators.Count(x => x.IsActive) <= 1)
            {
                throw new ValidationException(
                    "No se puede desactivar al último administrador activo del sistema.");
            }
        }

        user.IsActive = isActive;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(x => x.Description));

            throw new ValidationException(errors);
        }

        var securityStampResult = await _userManager.UpdateSecurityStampAsync(user);

        if (!securityStampResult.Succeeded)
        {
            var errors = string.Join(
                ", ",
                securityStampResult.Errors.Select(x => x.Description));

            throw new ValidationException(errors);
        }

        await transaction.CommitAsync();
    }

    public async Task<List<UserDto>> GetUsersAsync(
    CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var employee = user.EmployeeId.HasValue
                ? await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == user.EmployeeId.Value,
                        cancellationToken)
                : null;

            result.Add(new UserDto(
                user.Id,
                user.UserName ?? "",
                user.EmployeeId,
                employee?.PayrollNumber,
                employee?.FullName,
                user.IsActive,
                roles.ToList()));
        }

        return result;
    }

}