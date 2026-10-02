using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Contracts.Employees;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;
using PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;
using PermisosAdministrativos.Application.Features.Employees.Commands.SetEmployeeStatus;
using PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployees;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly GetEmployeesQueryHandler _getEmployeesHandler;
    private readonly CreateEmployeeCommandHandler _createEmployeeHandler;
    private readonly ImportEmployeesCommandHandler _importEmployeesHandler;
    private readonly SetEmployeeStatusCommandHandler _setStatusHandler;

    public EmployeesController(
        GetEmployeesQueryHandler getEmployeesHandler,
        CreateEmployeeCommandHandler createEmployeeHandler,
        ImportEmployeesCommandHandler importEmployeesHandler,
        SetEmployeeStatusCommandHandler setStatusHandler)
    {
        _getEmployeesHandler = getEmployeesHandler;
        _createEmployeeHandler = createEmployeeHandler;
        _importEmployeesHandler = importEmployeesHandler;
        _setStatusHandler = setStatusHandler;
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.HumanResources + "," + Roles.Supervisor)]
    [HttpGet]
    public async Task<IActionResult> GetAll(
    string? search,
    bool? isActive,
    int? departmentId,
    int page = 1,
    int pageSize = 25,
    CancellationToken cancellationToken = default)
    {
        var result = await _getEmployeesHandler.HandleAsync(
            new GetEmployeesQuery(
                search,
                isActive,
                departmentId,
                page,
                pageSize),
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost]
    public async Task<IActionResult> Create(
    CreateEmployeeCommand command,
    CancellationToken cancellationToken)
    {
        var id = await _createEmployeeHandler.HandleAsync(
            command,
            cancellationToken);

        return Ok(new { id });
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.HumanResources)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(
    [FromForm] ImportEmployeesRequest request,
    CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("Debes seleccionar un archivo.");

        if (!Path.GetExtension(request.File.FileName)
            .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Solo se permiten archivos .xlsx.");
        }

        const long maxFileSize = 5 * 1024 * 1024; // 5 MB

        if (request.File.Length > maxFileSize)
        {
            return BadRequest(
                "El archivo no puede superar los 5 MB.");
        }

        await using var stream = request.File.OpenReadStream();

        var result = await _importEmployeesHandler.HandleAsync(
            new ImportEmployeesCommand(stream),
            cancellationToken);

        return Ok(result);
    }

    [Authorize(Roles = Roles.Administrator + "," + Roles.HumanResources)]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(
    int id,
    SetEmployeeStatusRequest request,
    CancellationToken cancellationToken)
    {
        await _setStatusHandler.HandleAsync(
            new SetEmployeeStatusCommand(
                id,
                request.IsActive),
            cancellationToken);

        return NoContent();
    }

}