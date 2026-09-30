using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermisosAdministrativos.Api.Contracts.PersonalPermits;
using PermisosAdministrativos.Application.Authorization;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.ApprovePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CompletePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.CreatePersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Commands.RejectPersonalPermit;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetApprovedPersonalPermits;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPendingPersonalPermits;
using PermisosAdministrativos.Application.Features.PersonalPermits.Queries.GetPersonalPermits;
using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Api.Controllers;

[ApiController]
[Route("api/personal-permits")]
[Authorize]
public class PersonalPermitsController : ControllerBase
{
    private readonly CreatePersonalPermitCommandHandler _createHandler;
    private readonly GetPendingPersonalPermitsQueryHandler _getPendingHandler;
    private readonly ApprovePersonalPermitCommandHandler _approveHandler;
    private readonly RejectPersonalPermitCommandHandler _rejectHandler;
    private readonly GetApprovedPersonalPermitsQueryHandler _getApprovedHandler;
    private readonly CompletePersonalPermitCommandHandler _completeHandler;
    private readonly GetPersonalPermitsQueryHandler _getAllHandler;

    public PersonalPermitsController(
    CreatePersonalPermitCommandHandler createHandler,
    GetPendingPersonalPermitsQueryHandler getPendingHandler,
    ApprovePersonalPermitCommandHandler approveHandler,
    RejectPersonalPermitCommandHandler rejectHandler,
    GetApprovedPersonalPermitsQueryHandler getApprovedHandler,
    CompletePersonalPermitCommandHandler completeHandler,
    GetPersonalPermitsQueryHandler getAllHandler)
    {
        _createHandler = createHandler;
        _getPendingHandler = getPendingHandler;
        _approveHandler = approveHandler;
        _rejectHandler = rejectHandler;
        _getApprovedHandler = getApprovedHandler;
        _completeHandler = completeHandler;
        _getAllHandler = getAllHandler;
    }

    [Authorize(Roles = Roles.Supervisor + "," + Roles.Administrator)]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreatePersonalPermitCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _createHandler.HandleAsync(
            command,
            cancellationToken);

        return Ok(new { id });
    }

    [Authorize(Roles = Roles.HumanResources + "," + Roles.Administrator)]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
    CancellationToken cancellationToken)
    {
        var permits = await _getPendingHandler.HandleAsync(
            new GetPendingPersonalPermitsQuery(),
            cancellationToken);

        return Ok(permits);
    }

    [Authorize(Roles = Roles.HumanResources + "," + Roles.Administrator)]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(
    int id,
    CancellationToken cancellationToken)
    {
        await _approveHandler.HandleAsync(
            new ApprovePersonalPermitCommand(id),
            cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = Roles.HumanResources + "," + Roles.Administrator)]
    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(
    int id,
    RejectPersonalPermitRequest request,
    CancellationToken cancellationToken)
    {
        await _rejectHandler.HandleAsync(
            new RejectPersonalPermitCommand(
                id,
                request.Reason),
            cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = Roles.Security + "," + Roles.Administrator)]
    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved(
    CancellationToken cancellationToken)
    {
        var permits = await _getApprovedHandler.HandleAsync(
            new GetApprovedPersonalPermitsQuery(),
            cancellationToken);

        return Ok(permits);
    }

    [Authorize(Roles = Roles.Security + "," + Roles.Administrator)]
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(
    int id,
    CancellationToken cancellationToken)
    {
        await _completeHandler.HandleAsync(
            new CompletePersonalPermitCommand(id),
            cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = Roles.HumanResources + "," + Roles.Administrator)]
    [HttpGet]
    public async Task<IActionResult> GetAll(
    int? employeeId,
    PermitStatus? status,
    DateOnly? fromDate,
    DateOnly? toDate,
    int page = 1,
    int pageSize = 25,
    CancellationToken cancellationToken = default)
    {
        var permits = await _getAllHandler.HandleAsync(
            new GetPersonalPermitsQuery(
                employeeId,
                status,
                fromDate,
                toDate,
                page,
                pageSize),
            cancellationToken);

        return Ok(permits);
    }

}