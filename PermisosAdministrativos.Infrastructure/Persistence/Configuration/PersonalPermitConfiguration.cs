using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermisosAdministrativos.Domain.Entities;

namespace PermisosAdministrativos.Infrastructure.Persistence.Configurations;

public class PersonalPermitConfiguration
    : IEntityTypeConfiguration<PersonalPermit>
{
    public void Configure(EntityTypeBuilder<PersonalPermit> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.SupervisorApprovedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.HumanResourcesReviewedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.ExitRegisteredByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}