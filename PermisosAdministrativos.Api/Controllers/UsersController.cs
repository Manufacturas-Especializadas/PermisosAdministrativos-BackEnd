using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Contracts.Users;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Features.Users.Commands.CreateUser;
using PermisosAdministrativos.Application.Features.Users.Commands.SetUserStatus;
using PermisosAdministrativos.Application.Features.Users.Queries.GetUsers;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly CreateUserCommandHandler _createHandler;
    private readonly GetUsersQueryHandler _getUsersHandler;
    private readonly SetUserStatusCommandHandler _setStatusHandler;

    public UsersController(CreateUserCommandHandler createHandler,
        GetUsersQueryHandler getUsersHandler,
        SetUserStatusCommandHandler setStatusHandler)
    {
        _createHandler = createHandler;
        _getUsersHandler = getUsersHandler;
        _setStatusHandler = setStatusHandler;
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

}