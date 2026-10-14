using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermisosAdministrativos.Api.Contracts.Employees;
using PermisosAdministrativos.Api.Controllers;
using PermisosAdministrativos.Api.ExceptionHandling;
using PermisosAdministrativos.Application;
using PermisosAdministrativos.Application.Common.Models;
using PermisosAdministrativos.Application.DTOs;
using PermisosAdministrativos.Application.Features.Employees.Commands.CreateEmployee;
using PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;
using PermisosAdministrativos.Application.Features.Employees.Commands.SetEmployeeStatus;
using PermisosAdministrativos.Application.Features.Employees.Commands.UpdateEmployee;
using PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployeeById;
using PermisosAdministrativos.Application.Features.Employees.Queries.GetEmployees;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Infrastructure;
using PermisosAdministrativos.Infrastructure.Persistence;
using PermisosAdministrativos.Infrastructure.Repositories;
using PermisosAdministrativos.Infrastructure.Services;
using Xunit;

namespace PermisosAdministrativos.Tests;

public class EmployeesControllerTests : IDisposable
{
    private readonly EmployeeTestContext _context = new();

    private EmployeesController Controller()
    {
        var employees = new EmployeeRepository(_context);
        var departments = new DepartmentRepository(_context);
        return new EmployeesController(
            new GetEmployeesQueryHandler(employees),
            new CreateEmployeeCommandHandler(employees, departments),
            new ImportEmployeesCommandHandler(new EmployeeSpreadsheetReader(), employees, departments),
            new SetEmployeeStatusCommandHandler(employees),
            new GetEmployeeByIdQueryHandler(employees),
            new UpdateEmployeeCommandHandler(employees, departments));
    }

    [Fact]
    public async Task GetByIdReturns200AndCompleteEmployeeDto()
    {
        await _context.SeedAsync();
        var response = Assert.IsType<OkObjectResult>(await Controller().GetById(1, default));
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new EmployeeDto(1, "001", "Ana", 1, "Calidad", true), Assert.IsType<EmployeeDto>(response.Value));
    }

    [Fact]
    public async Task UpdateReturns204UsesRouteIdAndPreservesStatus()
    {
        await _context.SeedAsync(employeeActive: false);
        var request = JsonSerializer.Deserialize<UpdateEmployeeRequest>(
            """{"id":99,"isActive":true,"payrollNumber":" 001 ","fullName":" María ","departmentId":2}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var response = Assert.IsType<NoContentResult>(await Controller().Update(1, request, default));
        Assert.Equal(204, response.StatusCode);
        _context.ChangeTracker.Clear();
        var saved = await _context.Employees.SingleAsync();
        Assert.Equal(1, saved.Id);
        Assert.False(saved.IsActive);
        Assert.Equal("001", saved.PayrollNumber);
        Assert.Equal("María", saved.FullName);
        Assert.Equal(2, saved.DepartmentId);
    }

    [Fact]
    public async Task ExistingCreateListAndStatusContractsArePreserved()
    {
        await _context.SeedAsync(false);
        var controller = Controller();
        var created = Assert.IsType<OkObjectResult>(await controller.Create(new("001", "Ana", 1), default));
        var id = JsonSerializer.SerializeToElement(created.Value).GetProperty("id").GetInt32();
        Assert.True(id > 0);
        var listed = Assert.IsType<OkObjectResult>(await controller.GetAll(null, null, null));
        var page = Assert.IsType<PagedResult<EmployeeDto>>(listed.Value);
        Assert.Equal(1, page.Page);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.IsType<NoContentResult>(await controller.SetStatus(id, new(false), default));
        Assert.False((await _context.Employees.SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData("get-missing", 404)]
    [InlineData("update-missing", 404)]
    [InlineData("duplicate", 409)]
    [InlineData("invalid-fields", 400)]
    [InlineData("inactive-department", 400)]
    [InlineData("missing-department", 404)]
    public async Task ErrorsUseExistingProblemDetailsMapping(string scenario, int status)
    {
        await _context.SeedAsync();
        _context.Employees.Add(new Employee { PayrollNumber = "002", FullName = "Otro", DepartmentId = 1, IsActive = false });
        await _context.SaveChangesAsync();
        var controller = Controller();
        var error = await Record.ExceptionAsync(async () =>
        {
            if (scenario == "get-missing")
                await controller.GetById(99, default);
            else
                await controller.Update(scenario == "update-missing" ? 99 : 1,
                    new(scenario == "duplicate" ? "002" : "001", scenario == "invalid-fields" ? "" : "Ana",
                        scenario == "inactive-department" ? 3 : scenario == "missing-department" ? 99 : 1), default);
        });
        Assert.NotNull(error);
        await AssertProblemAsync(error, status);
    }

    [Theory]
    [InlineData("empleados.xlsx", 0)]
    [InlineData("empleados.xls", 1)]
    [InlineData("empleados.csv", 1)]
    [InlineData("empleados.xlsx", 5242881)]
    public async Task InvalidUploadReturns400(string filename, long length)
    {
        using var stream = new MemoryStream();
        var response = await Controller().Import(new() { File = new FormFile(stream, 0, length, "File", filename) }, default);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(response).StatusCode);
    }

    [Fact]
    public async Task MissingFileReturns400()
    {
        Assert.IsType<BadRequestObjectResult>(await Controller().Import(new(), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptWorkbookOrMissingHeaderReturns400ThroughExceptionHandler(bool corrupt)
    {
        using var stream = corrupt ? new MemoryStream("No es un Excel"u8.ToArray())
            : EmployeeSpreadsheetReaderTests.Excel(sheet => sheet.Cell(1, 1).Value = "Otro");
        var error = await Record.ExceptionAsync(() => Controller().Import(new()
        {
            File = new FormFile(stream, 0, stream.Length, "File", "empleados.xlsx")
        }, default));
        Assert.NotNull(error);
        await AssertProblemAsync(error, 400);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportReturns200WithOriginalSummaryContract(bool withData)
    {
        await _context.SeedAsync(false);
        using var stream = withData ? ImportEmployeesTests.ExcelRows(("001", "Ana", "Calidad"))
            : EmployeeSpreadsheetReaderTests.Excel();
        var response = Assert.IsType<OkObjectResult>(await Controller().Import(new()
        {
            File = new FormFile(stream, 0, stream.Length, "File", "empleados.XLSX")
        }, default));
        var result = Assert.IsType<EmployeeImportResult>(response.Value);
        var count = withData ? 1 : 0;
        Assert.Equal($"{{\"processed\":{count},\"created\":{count},\"ignored\":0,\"errors\":[]}}",
            JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    public static IEnumerable<object?[]> AuthorizationCases()
    {
        foreach (var action in new[] { "GetAll", "GetById", "Create", "Update", "Import", "SetStatus" })
        foreach (var role in new string?[] { null, "Administrator", "HumanResources", "Supervisor", "Security" })
        {
            var allowed = role == "Administrator"
                || role == "HumanResources" && action is "GetAll" or "GetById" or "Import" or "SetStatus"
                || role == "Supervisor" && action is "GetAll" or "GetById";
            yield return [action, role, allowed];
        }
    }

    [Theory]
    [MemberData(nameof(AuthorizationCases))]
    public async Task AuthorizationPreservesExistingRolesAndRestrictsNewEndpoints(string action, string? role, bool allowed)
    {
        var type = typeof(EmployeesController);
        var method = type.GetMethod(action)!;
        Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        using var provider = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            type.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()));
        Assert.NotNull(policy);
        var user = role is null ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public void RoutesExposeDetailAndUpdateButNoPhysicalDelete()
    {
        var type = typeof(EmployeesController);
        Assert.Equal("api/employees", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("{id:int}", type.GetMethod("GetById")!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{id:int}", type.GetMethod("Update")!.GetCustomAttribute<HttpPutAttribute>()!.Template);
        Assert.DoesNotContain(type.GetMethods(), method => method.GetCustomAttribute<HttpDeleteAttribute>() is not null);
    }

    [Fact]
    public void NewHandlersResolveFromExistingDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure("Server=unused;Database=unused");
        services.RemoveAll<ApplicationDbContext>();
        services.AddSingleton<ApplicationDbContext>(_context);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(ActivatorUtilities.CreateInstance<EmployeesController>(scope.ServiceProvider));
    }

    private static async Task AssertProblemAsync(Exception error, int status)
    {
        using var provider = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        using var stream = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Response.Body = stream;
        Assert.True(await new GlobalExceptionHandler().TryHandleAsync(http, error, default));
        Assert.Equal(status, http.Response.StatusCode);
        stream.Position = 0;
        using var json = await JsonDocument.ParseAsync(stream);
        Assert.Equal(error.Message, json.RootElement.GetProperty("title").GetString());
    }

    public void Dispose() => _context.Dispose();
}
