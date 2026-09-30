using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Departments.Queries.GetDepartments;

public class GetDepartmentsQueryHandler
{
    private readonly IDepartmentRepository _departmentRepository;

    public GetDepartmentsQueryHandler(
        IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    public Task<List<Department>> HandleAsync(
        GetDepartmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        return _departmentRepository.GetAllAsync(cancellationToken);
    }
}