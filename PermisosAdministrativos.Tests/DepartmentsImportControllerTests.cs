using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermisosAdministrativos.Api.Contracts.Departments;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Api.ExceptionHandling;
using PermisosAdministrativos.Application;
using PermisosAdministrativos.Application.Common.Exceptions;
using PermisosAdministrativos.Application.Features.Departments.Commands.CreateDepartment;
using PermisosAdministrativos.Application.Features.Departments.Commands.ImportDepartments;
using PermisosAdministrativos.Application.Features.Departments.Queries.GetDepartments;
using PermisosAdministrativos.Infrastructure;
using PermisosAdministrativos.Infrastructure.Persistence;
using PermisosAdministrativos.Infrastructure.Repositories;
using PermisosAdministrativos.Infrastructure.Services;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class DepartmentsImportControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private DepartmentsController Controller()
    {
        var repository = new DepartmentRepository(_context);
        return new DepartmentsController(
            new GetDepartmentsQueryHandler(repository),
            new CreateDepartmentCommandHandler(repository),
            new ImportDepartmentsCommandHandler(new DepartmentSpreadsheetReader(), repository));
    }

    [Fact]
    public async Task MissingFileReturns400()
    {
        var response = await Controller().Import(new(), default);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(response).StatusCode);
    }

    [Theory]
    [InlineData("departamentos.xlsx", 0, "Debes seleccionar un archivo.")]
    [InlineData("departamentos.xls", 1, "Solo se permiten archivos .xlsx.")]
    [InlineData("departamentos.csv", 1, "Solo se permiten archivos .xlsx.")]
    [InlineData("departamentos.xlsx", 5242881, "El archivo no puede superar los 5 MB.")]
    public async Task InvalidUploadReturns400(string fileName, long length, string message)
    {
        using var stream = new MemoryStream();
        var response = await Controller().Import(new()
        {
            File = new FormFile(stream, 0, length, "File", fileName)
        }, default);
        var error = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(message, error.Value);
        Assert.Empty(await _context.Departments.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidUploadReturns200WithEmployeeCompatibleJson(bool withData)
    {
        using var stream = DepartmentSpreadsheetReaderTests.Excel(sheet =>
        {
            sheet.Cell(1, 1).Value = "Departamento";
            if (withData)
                sheet.Cell(2, 1).Value = "Calidad";
        });
        var response = await Controller().Import(new()
        {
            File = new FormFile(stream, 0, stream.Length, "File", "departamentos.XLSX")
        }, default);
        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(200, ok.StatusCode);
        var result = Assert.IsType<DepartmentImportResult>(ok.Value);
        var count = withData ? 1 : 0;
        Assert.Equal(count, result.Processed);
        Assert.Equal(count, result.Created);
        Assert.Equal(0, result.Ignored);
        Assert.Empty(result.Errors);
        Assert.Equal($"{{\"processed\":{count},\"created\":{count},\"ignored\":0,\"errors\":[]}}",
            JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidWorkbookUsesExistingHandlerToReturn400(bool corrupt)
    {
        using var stream = corrupt
            ? new MemoryStream("Archivo corrupto"u8.ToArray())
            : DepartmentSpreadsheetReaderTests.Excel(sheet => sheet.Cell(1, 1).Value = "Nombre");
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Controller().Import(new()
        {
            File = new FormFile(stream, 0, stream.Length, "File", "departamentos.xlsx")
        }, default));
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        using var response = new MemoryStream();
        httpContext.Response.Body = response;
        Assert.True(await new GlobalExceptionHandler().TryHandleAsync(httpContext, exception, default));
        Assert.Equal(400, httpContext.Response.StatusCode);
        response.Position = 0;
        using var json = await JsonDocument.ParseAsync(response);
        Assert.Equal(exception.Message, json.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("HumanResources", false)]
    [InlineData("Supervisor", false)]
    [InlineData("Security", false)]
    [InlineData("Administrator", true)]
    public async Task AuthorizationMatchesManualCreation(string? role, bool allowed)
    {
        var controller = typeof(DepartmentsController);
        var import = controller.GetMethod(nameof(DepartmentsController.Import))!;
        var create = controller.GetMethod(nameof(DepartmentsController.Create))!;
        Assert.Equal("Administrator", import.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal(create.GetCustomAttribute<AuthorizeAttribute>()!.Roles,
            import.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(import.GetCustomAttributes<AllowAnonymousAttribute>());

        using var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var metadata = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(import.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(
            services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        Assert.NotNull(policy);
        var user = role is null ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
        var result = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, null, policy);
        Assert.Equal(allowed, result.Succeeded);
        Assert.Equal("api/departments", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("import", import.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Contains("multipart/form-data", import.GetCustomAttribute<ConsumesAttribute>()!.ContentTypes);
    }

    [Fact]
    public void ImportDependenciesAreRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure("Server=unused;Database=unused");
        services.RemoveAll<ApplicationDbContext>();
        services.AddSingleton(_context);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(ActivatorUtilities.CreateInstance<DepartmentsController>(scope.ServiceProvider));
    }

    public void Dispose() => _context.Dispose();
}
