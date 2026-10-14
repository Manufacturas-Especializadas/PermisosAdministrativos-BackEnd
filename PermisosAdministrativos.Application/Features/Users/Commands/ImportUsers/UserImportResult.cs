namespace PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;

public class UserImportResult
{
    public int Processed { get; set; }

    public int Created { get; set; }

    public int Ignored { get; set; }

    public List<string> Errors { get; set; } = [];
}