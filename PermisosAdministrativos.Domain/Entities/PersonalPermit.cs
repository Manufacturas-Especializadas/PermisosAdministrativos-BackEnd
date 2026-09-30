using PermisosAdministrativos.Domain.Enums;

namespace PermisosAdministrativos.Domain.Entities;

public class PersonalPermit
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public DateOnly PermitDate { get; set; }

    public TimeOnly ExitTime { get; set; }

    public PersonalPermitType PermitType { get; set; }

    public string Reason { get; set; } = string.Empty;

    public PermitStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime SupervisorApprovedAt { get; set; }

    public string SupervisorApprovedByUserId { get; set; } = string.Empty;

    public DateTime? HumanResourcesReviewedAt { get; set; }

    public string? HumanResourcesReviewedByUserId { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? ActualExitAt { get; set; }

    public string? ExitRegisteredByUserId { get; set; }
}
