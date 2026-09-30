using Microsoft.AspNetCore.Identity;

namespace PermisosAdministrativos.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public int? EmployeeId { get; set; }

    public bool IsActive { get; set; } = true;
}