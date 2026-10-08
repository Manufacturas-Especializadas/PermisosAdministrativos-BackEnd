using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Contracts.Departments;
using PermisosAdministrativos.Application.Features.Departments.Commands.CreateDepartment;
using PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;
using PermisosAdministrativos.Application.Features.Departments.Queries.GetDepartments;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly GetDepartmentsQueryHandler _getDepartmentsHandler;
    private readonly CreateDepartmentCommandHandler _createDepartmentHandler;
    private readonly ImportDepartmentsCommandHandler _importDepartmentsHandler;

    public DepartmentsController(
        GetDepartmentsQueryHandler getDepartmentsHandler,
        CreateDepartmentCommandHandler createDepartmentHandler,
        ImportDepartmentsCommandHandler importDepartmentsHandler)
    {
        _getDepartmentsHandler = getDepartmentsHandler;
        _createDepartmentHandler = createDepartmentHandler;
        _importDepartmentsHandler = importDepartmentsHandler;
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(
        [FromForm] ImportDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("Debes seleccionar un archivo.");

        if (!Path.GetExtension(request.File.FileName)
            .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Solo se permiten archivos .xlsx.");
        }

        const long maxFileSize = 5 * 1024 * 1024;
        if (request.File.Length > maxFileSize)
            return BadRequest("El archivo no puede superar los 5 MB.");

        await using var stream = request.File.OpenReadStream();
        var result = await _importDepartmentsHandler.HandleAsync(
            new ImportDepartmentsCommand(stream), cancellationToken);

        return Ok(result);
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
