using System.ComponentModel.DataAnnotations;

namespace PermisosAdministrativos.Application.DTOs.Auth;

public record ChangePasswordRequest(
    [param: Required] string CurrentPassword,
    [param: Required] string NewPassword);
