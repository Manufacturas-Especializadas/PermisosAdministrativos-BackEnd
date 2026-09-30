namespace PermisosAdministrativos.Api.Contracts.Employees;

public class ImportEmployeesRequest
{
    public IFormFile File { get; set; } = default!;
}
