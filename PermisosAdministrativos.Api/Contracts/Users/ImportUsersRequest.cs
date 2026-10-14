namespace PermisosAdministrativos.Api.Contracts.Users;

public class ImportUsersRequest
{
    public IFormFile File { get; set; } = default!;
}