using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Contracts.Users;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Features.Users.Commands.CreateUser;
using PermisosAdministrativos.Application.Features.Users.Commands.ImportUsers;
using PermisosAdministrativos.Application.Features.Users.Commands.SetUserStatus;
using PermisosAdministrativos.Application.Features.Users.Queries.GetUsers;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.PasswordChanged)]
[Authorize(Roles = Roles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly CreateUserCommandHandler _createHandler;
    private readonly GetUsersQueryHandler _getUsersHandler;
    private readonly SetUserStatusCommandHandler _setStatusHandler;
    private readonly ImportUsersCommandHandler _importUsersHandler;

    public UsersController(CreateUserCommandHandler createHandler,
        GetUsersQueryHandler getUsersHandler,
        SetUserStatusCommandHandler setStatusHandler,
        ImportUsersCommandHandler importUsersHandler)
    {
        _createHandler = createHandler;
        _getUsersHandler = getUsersHandler;
        _setStatusHandler = setStatusHandler;
        _importUsersHandler = importUsersHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await _createHandler.HandleAsync(
            new CreateUserCommand(
                request.UserName,
                request.Password,
                request.EmployeeId,
                request.Role),
            cancellationToken);

        return Ok(new { userId });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> SetStatus(
        string id,
        SetUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        await _setStatusHandler.HandleAsync(
            new SetUserStatusCommand(
                id,
                request.IsActive),
            cancellationToken);

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
    CancellationToken cancellationToken)
    {
        var users = await _getUsersHandler.HandleAsync(
            new GetUsersQuery(),
            cancellationToken);

        return Ok(users);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(
    [FromForm] ImportUsersRequest request,
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
        {
            return BadRequest(
                "El archivo no puede superar los 5 MB.");
        }

        await using var stream =
            request.File.OpenReadStream();

        var result = await _importUsersHandler.HandleAsync(
            new ImportUsersCommand(stream),
            cancellationToken);

        return Ok(result);
    }

}