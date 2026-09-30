using PermisosAdministrativos.Application.Interfaces;
using PermisosAdministrativos.Domain.Entities;
using PermisosAdministrativos.Domain.Interfaces;

namespace PermisosAdministrativos.Application.Features.Employees.Commands.ImportEmployees;

public class ImportEmployeesCommandHandler
{
    private readonly IEmployeeSpreadsheetReader _spreadsheetReader;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;

    public ImportEmployeesCommandHandler(
        IEmployeeSpreadsheetReader spreadsheetReader,
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository)
    {
        _spreadsheetReader = spreadsheetReader;
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
    }

    public async Task<EmployeeImportResult> HandleAsync(
        ImportEmployeesCommand command,
        CancellationToken cancellationToken = default)
    {
        var rows = await _spreadsheetReader.ReadAsync(
            command.FileStream,
            cancellationToken);

        var result = new EmployeeImportResult
        {
            Processed = rows.Count
        };

        var departments = await _departmentRepository
            .GetAllAsync(cancellationToken);

        var departmentsByName = departments
            .Where(x => x.IsActive)
            .ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);

        var payrollNumbers = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.PayrollNumber))
            .Select(x => x.PayrollNumber.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingPayrollNumbers =
            await _employeeRepository.GetExistingPayrollNumbersAsync(
                payrollNumbers,
                cancellationToken);

        var existing = new HashSet<string>(
            existingPayrollNumbers,
            StringComparer.OrdinalIgnoreCase);

        var payrollNumbersInFile =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var employeesToCreate = new List<Employee>();

        foreach (var row in rows)
        {
            var payrollNumber = row.PayrollNumber.Trim();
            var fullName = row.FullName.Trim();
            var departmentName = row.DepartmentName.Trim();

            if (string.IsNullOrWhiteSpace(payrollNumber) ||
                string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(departmentName))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: faltan datos obligatorios.");

                continue;
            }

            if (!payrollNumbersInFile.Add(payrollNumber))
            {
                result.Ignored++;
                continue;
            }

            if (existing.Contains(payrollNumber))
            {
                result.Ignored++;
                continue;
            }

            if (!departmentsByName.TryGetValue(
                    departmentName,
                    out var department))
            {
                result.Errors.Add(
                    $"Fila {row.RowNumber}: el departamento '{departmentName}' no existe.");

                continue;
            }

            employeesToCreate.Add(new Employee
            {
                PayrollNumber = payrollNumber,
                FullName = fullName,
                DepartmentId = department.Id,
                IsActive = true
            });
        }

        if (employeesToCreate.Count > 0)
        {
            await _employeeRepository.AddRangeAsync(
                employeesToCreate,
                cancellationToken);
        }

        result.Created = employeesToCreate.Count;

        return result;
    }
}