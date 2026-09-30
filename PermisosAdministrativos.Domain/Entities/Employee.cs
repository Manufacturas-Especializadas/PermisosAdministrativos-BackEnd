namespace PermisosAdministrativos.Domain.Entities;

public class Employee
{
    public int Id { get; set; }

    public string PayrollNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int DepartmentId { get; set; }

    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;
}