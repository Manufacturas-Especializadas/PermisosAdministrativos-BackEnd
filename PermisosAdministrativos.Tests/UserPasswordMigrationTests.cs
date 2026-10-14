using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PermisosAdministrativos.Infrastructure.Identity;
using PermisosAdministrativos.Infrastructure.Migrations;
using PermisosAdministrativos.Infrastructure.Persistence;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class UserPasswordMigrationTests
{
    [Fact]
    public void MigrationOnlyAddsRequiredFlagWithFalseDefaultForExistingRows()
    {
        Migration migration = new AddUserMustChangePassword();
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("AspNetUsers", column.Table);
        Assert.Equal("MustChangePassword", column.Name);
        Assert.Equal("bit", column.ColumnType);
        Assert.Equal(typeof(bool), column.ClrType);
        Assert.False(column.IsNullable);
        Assert.Equal(false, column.DefaultValue);
        var down = Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal("AspNetUsers", down.Table);
        Assert.Equal("MustChangePassword", down.Name);
    }

    [Fact]
    public void SqlServerModelAndMigrationSnapshotHaveNoPendingSchemaChanges()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=unused;Database=unused").Options);
        var property = context.Model.FindEntityType(typeof(ApplicationUser))!.FindProperty(nameof(ApplicationUser.MustChangePassword))!;
        Assert.Equal(false, property.GetDefaultValue());
        Assert.False(property.IsNullable);
        // Model comparison only: no connection is opened and no migration is applied.
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
