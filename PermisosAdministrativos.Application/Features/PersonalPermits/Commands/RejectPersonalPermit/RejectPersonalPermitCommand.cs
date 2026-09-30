namespace PermisosAdministrativos.Application.Features.PersonalPermits.Commands.RejectPersonalPermit;

public record RejectPersonalPermitCommand(
    int PermitId,
    string Reason);