using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Contracts.Users;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Features.Users.Commands.CreateUser;
using PermisosAdministrativos.Application.Features.Users.Queries.GetUsers;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly CreateUserCommandHandler _createHandler;
    private readonly GetUsersQueryHandler _getUsersHandler;

    public UsersController(CreateUserCommandHandler createHandler,
        GetUsersQueryHandler getUsersHandler)
    {
        _createHandler = createHandler;
        _getUsersHandler = getUsersHandler;
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