using Microsoft.AspNetCore.Identity;

namespace PermisosAdministrativos.Infrastructure.Identity;

public class MaximumLengthPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user,
        string? password)
    {
        return Task.FromResult(password is { Length: > 64 }
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = "La contraseña no puede exceder 64 caracteres."
            })
            : IdentityResult.Success);
    }
}
