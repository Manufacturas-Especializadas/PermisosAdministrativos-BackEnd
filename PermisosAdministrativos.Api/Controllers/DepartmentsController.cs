using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Application.Features.Departments.Commands.CreateDepartment;
using PermisosAdministrativos.Application.Features.Departments.Queries.GetDepartments;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly GetDepartmentsQueryHandler _getDepartmentsHandler;
    private readonly CreateDepartmentCommandHandler _createDepartmentHandler;

    public DepartmentsController(
        GetDepartmentsQueryHandler getDepartmentsHandler,
        CreateDepartmentCommandHandler createDepartmentHandler)
    {
        _getDepartmentsHandler = getDepartmentsHandler;
        _createDepartmentHandler = createDepartmentHandler;
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var departments = await _getDepartmentsHandler.HandleAsync(
            new GetDepartmentsQuery(),
            cancellationToken);

        return Ok(departments);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _createDepartmentHandler.HandleAsync(
            command,
            cancellationToken);

        return Ok(new { id });
    }
}