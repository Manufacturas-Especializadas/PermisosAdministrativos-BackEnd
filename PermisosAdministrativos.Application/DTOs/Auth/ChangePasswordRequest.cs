using System.ComponentModel.DataAnnotations;

namespace PermisosAdministrativos.Application.DTOs.Auth;

public record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required] string NewPassword);
