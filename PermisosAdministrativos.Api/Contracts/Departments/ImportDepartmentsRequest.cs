namespace PermisosAdministrativos.Api.Contracts.Departments;

public class ImportDepartmentsRequest
{
    public IFormFile File { get; set; } = default!;
}
