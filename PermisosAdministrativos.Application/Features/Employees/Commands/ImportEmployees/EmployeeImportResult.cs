namespace PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;

public class EmployeeImportResult
{
    public int Processed { get; set; }

    public int Created { get; set; }

    public int Ignored { get; set; }

    public List<string> Errors { get; set; } = [];
}