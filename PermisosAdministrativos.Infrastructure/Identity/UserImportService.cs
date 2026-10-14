using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;
using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Infrastructure.Persistence;

namespace PermisosAdministrativos.Infrastructure.Identity;

public class UserImportService : IUserImportService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public UserImportService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _configuration = configuration;
    }

    public async Task<UserImportResult> ImportAsync(
        IReadOnlyList<UserImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var result = new UserImportResult
        {
            Processed = rows.Count
        };

        var temporaryPassword =
            _configuration["UsersImport:TemporaryPassword"];

        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            throw new ValidationException(
                "No se ha configurado la contraseña temporal para la importación de usuarios.");
        }

        await ValidateTemporaryPasswordAsync(
            temporaryPassword,
            cancellationToken);

        var validRoles = new HashSet<string>(
        [
            Roles.Administrator,
            Roles.Supervisor,
            Roles.HumanResources,
            Roles.Security
        ],
        StringComparer.Ordinal);

        var existingRoles = await _roleManager.Roles
            .Where(x => x.Name != null && validRoles.Contains(x.Name))
            .Select(x => x.Name!)
            .ToListAsync(cancellationToken);

        var existingRoleSet = new HashSet<string>(
            existingRoles,
            StringComparer.Ordinal);

        var payrollNumbers = rows
            .Select(x => x.PayrollNumber.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var employees = await _context.Employees
            .AsNoTracking()
            .Where(x => payrollNumbers.Contains(x.PayrollNumber))
            .ToListAsync(cancellationToken);

        var employeesByPayroll = employees.ToDictionary(
            x => x.PayrollNumber,
            StringComparer.OrdinalIgnoreCase);

        var normalizedUserNames = payrollNumbers
            .Select(x => _userManager.NormalizeName(x))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingUsers = await _userManager.Users
            .AsNoTracking()
            .Where(x =>
                x.NormalizedUserName != null &&
                normalizedUserNames.Contains(x.NormalizedUserName))
            .Select(x => new
            {
                x.NormalizedUserName,
                x.EmployeeId
            })
            .ToListAsync(cancellationToken);

        var existingUserNames = new HashSet<string>(
            existingUsers
                .Where(x => x.NormalizedUserName != null)
                .Select(x => x.NormalizedUserName!),
            StringComparer.OrdinalIgnoreCase);

        var linkedEmployeeIds = new HashSet<int>(
            existingUsers
                .Where(x => x.EmployeeId.HasValue)
                .Select(x => x.EmployeeId!.Value));

        var payrollNumbersInFile =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payrollNumber = row.PayrollNumber.Trim();
            var role = row.Role.Trim();

            if (string.IsNullOrWhiteSpace(payrollNumber) ||
                string.IsNullOrWhiteSpace(role))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: el número de nómina y el rol son obligatorios.");

                continue;
            }

            if (!existingRoleSet.Contains(role))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: el rol '{role}' no existe.");

                continue;
            }

            if (!employeesByPayroll.TryGetValue(
                    payrollNumber,
                    out var employee))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: el empleado con número de nómina '{payrollNumber}' no existe.");

                continue;
            }

            if (!employee.IsActive)
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: el empleado con número de nómina '{payrollNumber}' está inactivo.");

                continue;
            }

            if (!payrollNumbersInFile.Add(payrollNumber))
            {
                result.Ignored++;
                continue;
            }

            var normalizedUserName =
                _userManager.NormalizeName(payrollNumber);

            if (normalizedUserName is not null &&
                existingUserNames.Contains(normalizedUserName))
            {
                result.Ignored++;
                continue;
            }

            if (linkedEmployeeIds.Contains(employee.Id))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: el empleado con número de nómina '{payrollNumber}' ya está vinculado a otro usuario.");

                continue;
            }

            var creationResult = await CreateUserAsync(
                payrollNumber,
                employee.Id,
                role,
                temporaryPassword,
                cancellationToken);

            switch (creationResult.Status)
            {
                case UserImportRowStatus.Created:
                    result.Created++;

                    if (normalizedUserName is not null)
                        existingUserNames.Add(normalizedUserName);

                    linkedEmployeeIds.Add(employee.Id);

                    break;

                case UserImportRowStatus.Ignored:
                    result.Ignored++;
                    break;

                case UserImportRowStatus.Error:
                    result.Errors.Add(
                        $"Fila {row.RowNumber}: {creationResult.Message}");
                    break;
            }
        }

        return result;
    }

    private async Task<UserImportRowResult> CreateUserAsync(
        string userName,
        int employeeId,
        string role,
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var normalizedUserName =
                _userManager.NormalizeName(userName);

            var existingUser = await _userManager.Users
                .FirstOrDefaultAsync(
                    x => x.NormalizedUserName == normalizedUserName,
                    cancellationToken);

            if (existingUser is not null)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new UserImportRowResult(
                    UserImportRowStatus.Ignored,
                    "El usuario ya existe.");
            }

            var employeeAlreadyLinked =
                await _userManager.Users.AnyAsync(
                    x => x.EmployeeId == employeeId,
                    cancellationToken);

            if (employeeAlreadyLinked)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new UserImportRowResult(
                    UserImportRowStatus.Error,
                    "El empleado ya está vinculado a otro usuario.");
            }

            var user = new ApplicationUser
            {
                UserName = userName,
                EmployeeId = employeeId,
                IsActive = true,
                MustChangePassword = true
            };

            var createResult = await _userManager.CreateAsync(
                user,
                temporaryPassword);

            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new UserImportRowResult(
                    UserImportRowStatus.Error,
                    string.Join(
                        " ",
                        createResult.Errors.Select(x => x.Description)));
            }

            var roleResult = await _userManager.AddToRoleAsync(
                user,
                role);

            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return new UserImportRowResult(
                    UserImportRowStatus.Error,
                    string.Join(
                        " ",
                        roleResult.Errors.Select(x => x.Description)));
            }

            await transaction.CommitAsync(cancellationToken);

            return new UserImportRowResult(
                UserImportRowStatus.Created);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            _context.ChangeTracker.Clear();

            return new UserImportRowResult(
                UserImportRowStatus.Error,
                "No se pudo crear el usuario debido a un conflicto de datos.");
        }
    }

    private async Task ValidateTemporaryPasswordAsync(
        string temporaryPassword,
        CancellationToken cancellationToken)
    {
        var validationUser = new ApplicationUser
        {
            UserName = "TemporaryPasswordValidation"
        };

        foreach (var validator in _userManager.PasswordValidators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validationResult = await validator.ValidateAsync(
                _userManager,
                validationUser,
                temporaryPassword);

            if (!validationResult.Succeeded)
            {
                throw new ValidationException(
                    string.Join(
                        " ",
                        validationResult.Errors.Select(
                            x => x.Description)));
            }
        }
    }
}