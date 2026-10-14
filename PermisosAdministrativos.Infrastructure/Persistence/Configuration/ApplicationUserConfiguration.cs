using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Infrastructure.Identity;

namespace PermisosAdministrativos.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.MustChangePassword)
            .HasDefaultValue(false);

        builder.HasOne<Employee>()
            .WithOne()
            .HasForeignKey<ApplicationUser>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
