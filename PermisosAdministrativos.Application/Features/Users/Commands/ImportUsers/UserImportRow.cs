namespace PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;

public record UserImportRow(
    int RowNumber,
    string PayrollNumber,
    string Role);