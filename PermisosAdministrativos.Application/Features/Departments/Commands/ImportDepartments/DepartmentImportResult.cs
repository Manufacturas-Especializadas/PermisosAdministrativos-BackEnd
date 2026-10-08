namespace PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;

public class DepartmentImportResult
{
    public int Processed { get; set; }

    public int Created { get; set; }

    public int Ignored { get; set; }

    public List<string> Errors { get; set; } = [];
}
