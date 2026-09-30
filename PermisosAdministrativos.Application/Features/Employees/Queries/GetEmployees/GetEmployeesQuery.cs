namespace PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployees;

public record GetEmployeesQuery(
    string? Search,
    bool? IsActive,
    int? DepartmentId,
    int Page,
    int PageSize);