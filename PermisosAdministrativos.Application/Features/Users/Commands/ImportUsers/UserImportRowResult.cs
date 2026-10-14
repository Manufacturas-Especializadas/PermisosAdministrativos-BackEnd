namespace PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;

public enum UserImportRowStatus
{
    Created,
    Ignored,
    Error
}

public record UserImportRowResult(
    UserImportRowStatus Status,
    string? Message = null);