using PermisosAdministrativos.Application.Interfaces;

namespace PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;

public class ImportUsersCommandHandler
{
    private readonly IUserSpreadsheetReader _spreadsheetReader;
    private readonly IUserImportService _userImportService;

    public ImportUsersCommandHandler(
        IUserSpreadsheetReader spreadsheetReader,
        IUserImportService userImportService)
    {
        _spreadsheetReader = spreadsheetReader;
        _userImportService = userImportService;
    }

    public async Task<UserImportResult> HandleAsync(
        ImportUsersCommand command,
        CancellationToken cancellationToken = default)
    {
        var rows = await _spreadsheetReader.ReadAsync(
            command.FileStream,
            cancellationToken);

        return await _userImportService.ImportAsync(
            rows,
            cancellationToken);
    }
}